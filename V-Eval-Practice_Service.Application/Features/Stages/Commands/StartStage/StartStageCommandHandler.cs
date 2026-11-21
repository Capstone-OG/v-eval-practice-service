using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.StartStage;

public class StartStageCommandHandler : IRequestHandler<StartStageCommand, Result<StartStageResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly ILearningProfileRepository _learningProfileRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<StartStageCommandHandler> _logger;

    public StartStageCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningRoadmapRepository roadmapRepository,
        ILearningProfileRepository learningProfileRepository,
        IPaymentRepository paymentRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<StartStageCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _roadmapRepository = roadmapRepository;
        _learningProfileRepository = learningProfileRepository;
        _paymentRepository = paymentRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<StartStageResponseDto>> Handle(StartStageCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Khởi tạo chặng học P-L-A-R cho học sinh {StudentId}, chặng {NodeId}",
            request.StudentId, request.RoadmapNodeId);

        // 1. Kiểm tra tồn tại và quyền sở hữu của chặng học RoadmapNode
        var node = await _roadmapRepository.GetNodeByIdAsync(request.RoadmapNodeId, ct);
        if (node == null)
        {
            return Result<StartStageResponseDto>.Failure(
                Error.NotFound("RoadmapNode.NotFound", $"Không tìm thấy chặng học {request.RoadmapNodeId}"));
        }

        if (node.Roadmap != null && node.Roadmap.StudentId != request.StudentId)
        {
            return Result<StartStageResponseDto>.Failure(
                Error.Forbidden("RoadmapNode.Forbidden", "Bạn không có quyền truy cập chặng học của học sinh khác."));
        }

        // 2. Cổng kiểm soát học phí thực tế ở Backend (Bắt buộc đóng học phí để mở khóa PLAR)
        bool hasPaidTuition = await _paymentRepository.HasActiveTuitionAsync(request.StudentId, ct);
        if (!hasPaidTuition)
        {
            return Result<StartStageResponseDto>.Failure(
                Error.Forbidden("Tuition.Required",
                    "Vui lòng hoàn tất thanh toán học phí khóa học để mở khóa chu trình tự học thích ứng P-L-A-R trên lộ trình cá nhân."));
        }

        // 3. Kiểm tra trạng thái mở khóa chặng học trên lộ trình
        // Học sinh đã đóng học phí được phép tự do mở chu trình P-L-A-R trên mọi chặng GDPT 2018 đang hoạt động (trừ SKIPPED_PRUNED)
        if (string.Equals(node.Status, "SKIPPED_PRUNED", StringComparison.OrdinalIgnoreCase))
        {
            return Result<StartStageResponseDto>.Failure(
                Error.Validation("RoadmapNode.Pruned",
                    $"Chặng học #{node.StepOrder} đã được rút gọn khỏi lộ trình hiện tại."));
        }

        if (string.Equals(node.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Status, "UNLOCKED", StringComparison.OrdinalIgnoreCase))
        {
            node.Status = "IN_PROGRESS";
            node.UnlockedAt ??= DateTime.UtcNow;
            await _roadmapRepository.SaveChangesAsync(ct);
        }

        // 4. Tìm hoặc khởi tạo StageProgress với tiên nghiệm BKT P(L0) thật từ LearningProfiles
        var progress = await _stageProgressRepository.GetByStudentAndNodeAsync(request.StudentId, request.RoadmapNodeId, ct);
        if (progress == null)
        {
            double initialPlt = 0.2500;
            try
            {
                var profiles = await _learningProfileRepository.GetByStudentIdAsync(request.StudentId, ct);
                var matched = profiles.FirstOrDefault(p => p.SkillId == node.SkillId);
                if (matched != null && matched.MasteryScore > 0)
                {
                    initialPlt = Math.Round(Math.Clamp(matched.MasteryScore, 0.10, 0.70), 4);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc P(L0) từ LearningProfiles, sử dụng mặc định 0.25.");
            }

            progress = new StageProgress
            {
                Id = Guid.NewGuid(),
                StudentId = request.StudentId,
                RoadmapNodeId = request.RoadmapNodeId,
                CurrentStep = "PREVIEW",
                Status = "IN_PROGRESS",
                VideoWatchPercentage = 0.00m,
                BktMasteryPlt = initialPlt,
                ConsecutiveAdvancedCorrect = 0,
                ConsecutiveIncorrect = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _stageProgressRepository.AddAsync(progress, ct);
            _logger.LogInformation("Đã tạo mới tiến trình StageProgress {ProgressId} (P(L0)={Plt}) cho học sinh {StudentId}",
                progress.Id, initialPlt, request.StudentId);
        }

        // 5. Nạp 3 câu hỏi Quick Check thật từ ngân hàng câu hỏi (ReadOnly = true -> KHÔNG spam MockExam rác!)
        var previewQuestions = new List<PreviewQuestionDto>();
        try
        {
            var quizResult = await _contentGrpcClient.GetMilestoneQuizReadOnlyAsync(node.SkillId, node.QuizExamId, questionCount: 3, ct);
            if (quizResult != null && quizResult.Questions.Count > 0)
            {
                int order = 1;
                foreach (var q in quizResult.Questions.Take(3))
                {
                    previewQuestions.Add(new PreviewQuestionDto(
                        q.QuestionId,
                        order++,
                        q.Content,
                        q.Options.Select(o => new PreviewQuestionOptionDto(o.OptionId, o.Content)).ToList(),
                        q.DifficultyLevel,
                        q.SkillId,
                        q.SkillName,
                        q.PassageTitle,
                        q.PassageContent,
                        q.PassageImageUrl
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy câu hỏi Preview từ Content Service qua gRPC.");
        }

        var response = new StartStageResponseDto(
            progress.Id,
            progress.RoadmapNodeId,
            node.SkillId,
            progress.CurrentStep,
            progress.Status,
            progress.VideoWatchPercentage,
            progress.BktMasteryPlt,
            progress.ConsecutiveAdvancedCorrect,
            progress.ConsecutiveIncorrect,
            previewQuestions,
            progress.ConfidenceRating,
            progress.LearnedSummary,
            progress.MistakeNotes
        );

        return Result<StartStageResponseDto>.Success(response);
    }
}
