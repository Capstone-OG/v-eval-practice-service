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
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<StartStageCommandHandler> _logger;

    public StartStageCommandHandler(
        IStageProgressRepository stageProgressRepository,
        ILearningRoadmapRepository roadmapRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<StartStageCommandHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _roadmapRepository = roadmapRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<StartStageResponseDto>> Handle(StartStageCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Khởi tạo chặng học P-L-A-R cho học sinh {StudentId}, chặng {NodeId}",
            request.StudentId, request.RoadmapNodeId);

        // 1. Kiểm tra tồn tại của chặng học RoadmapNode
        var node = await _roadmapRepository.GetNodeByIdAsync(request.RoadmapNodeId, ct);
        if (node == null)
        {
            return Result<StartStageResponseDto>.Failure(
                Error.NotFound("RoadmapNode.NotFound", $"Không tìm thấy chặng học {request.RoadmapNodeId}"));
        }

        // 2. Tìm hoặc khởi tạo StageProgress của học sinh
        var progress = await _stageProgressRepository.GetByStudentAndNodeAsync(request.StudentId, request.RoadmapNodeId, ct);
        if (progress == null)
        {
            progress = new StageProgress
            {
                Id = Guid.NewGuid(),
                StudentId = request.StudentId,
                RoadmapNodeId = request.RoadmapNodeId,
                CurrentStep = "PREVIEW",
                Status = "IN_PROGRESS",
                VideoWatchPercentage = 0.00m,
                BktMasteryPlt = 0.1000,
                ConsecutiveAdvancedCorrect = 0,
                ConsecutiveIncorrect = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _stageProgressRepository.AddAsync(progress, ct);
            _logger.LogInformation("Đã tạo mới tiến trình StageProgress {ProgressId} cho học sinh {StudentId}",
                progress.Id, request.StudentId);
        }

        // 3. Nạp 3 câu hỏi Quick Check khởi động cho bước PREVIEW
        var previewQuestions = new List<PreviewQuestionDto>();
        try
        {
            var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(node.SkillId, node.QuizExamId, questionCount: 3, ct);
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
                        q.SkillName
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy câu hỏi Preview từ Content Service qua gRPC, sử dụng câu hỏi khởi động mặc định.");
        }

        // Fallback tạo 3 câu hỏi mẫu nếu chưa có từ Content Service để đảm bảo API luôn phản hồi mượt mà
        if (previewQuestions.Count == 0)
        {
            previewQuestions.AddRange(GetDefaultPreviewQuestions(node.SkillId));
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
            previewQuestions
        );

        return Result<StartStageResponseDto>.Success(response);
    }

    private static List<PreviewQuestionDto> GetDefaultPreviewQuestions(Guid skillId)
    {
        return new List<PreviewQuestionDto>
        {
            new(
                Guid.NewGuid(),
                1,
                "Khởi động 1: Khái niệm cơ bản của dạng bài này là gì?",
                new List<PreviewQuestionOptionDto>
                {
                    new("A", "Định nghĩa và tính chất cơ sở"),
                    new("B", "Phương pháp mở rộng gián tiếp"),
                    new("C", "Biến thể đặc biệt"),
                    new("D", "Trường hợp ngoại lệ")
                },
                1,
                skillId,
                "Kiến thức nền tảng"
            ),
            new(
                Guid.NewGuid(),
                2,
                "Khởi động 2: Công thức hoặc nguyên lý tiên quyết nào cần nhớ khi xử lý dạng này?",
                new List<PreviewQuestionOptionDto>
                {
                    new("A", "Áp dụng định lý trực tiếp"),
                    new("B", "Quy tắc loại trừ"),
                    new("C", "Phương pháp thế đại số"),
                    new("D", "Tất cả các phương pháp trên")
                },
                2,
                skillId,
                "Nguyên lý tiên quyết"
            ),
            new(
                Guid.NewGuid(),
                3,
                "Khởi động 3: Dấu hiệu nhận biết dạng bài thường xuất hiện trong đề thi ĐGNL là gì?",
                new List<PreviewQuestionOptionDto>
                {
                    new("A", "Dữ kiện dạng bảng hoặc biểu đồ phân tích"),
                    new("B", "Bài toán có từ khóa điều kiện ràng buộc logic"),
                    new("C", "Dữ kiện thực tế tích hợp liên môn"),
                    new("D", "Cả A, B và C")
                },
                2,
                skillId,
                "Dấu hiệu nhận biết"
            )
        };
    }
}
