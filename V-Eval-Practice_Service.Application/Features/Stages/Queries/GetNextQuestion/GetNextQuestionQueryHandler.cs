using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Adaptive;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Queries.GetNextQuestion;

public class GetNextQuestionQueryHandler : IRequestHandler<GetNextQuestionQuery, Result<NextQuestionResponseDto>>
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IZpdQuestionSelector _zpdQuestionSelector;
    private readonly ILogger<GetNextQuestionQueryHandler> _logger;

    public GetNextQuestionQueryHandler(
        IStageProgressRepository stageProgressRepository,
        IContentGrpcClient contentGrpcClient,
        IZpdQuestionSelector zpdQuestionSelector,
        ILogger<GetNextQuestionQueryHandler> logger)
    {
        _stageProgressRepository = stageProgressRepository;
        _contentGrpcClient = contentGrpcClient;
        _zpdQuestionSelector = zpdQuestionSelector;
        _logger = logger;
    }

    public async Task<Result<NextQuestionResponseDto>> Handle(GetNextQuestionQuery request, CancellationToken ct)
    {
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<NextQuestionResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        int currentAttemptCount = progress.AdaptiveAttempts?.Count ?? 0;

        if (progress.Status == "COMPLETED" || progress.CurrentStep == "REFLECT")
        {
            return Result<NextQuestionResponseDto>.Success(new NextQuestionResponseDto(
                progress.Id,
                progress.CurrentStep,
                progress.Status,
                progress.BktMasteryPlt,
                currentAttemptCount,
                IsFinished: true,
                Message: "Chúc mừng! Bạn đã đạt độ thành thạo mục tiêu cho chặng học này. Hãy tiến hành bước REFLECT để hoàn thành."
            ));
        }

        if (progress.Status == "REMEDIAL_REQUIRED")
        {
            return Result<NextQuestionResponseDto>.Success(new NextQuestionResponseDto(
                progress.Id,
                progress.CurrentStep,
                progress.Status,
                progress.BktMasteryPlt,
                currentAttemptCount,
                IsFinished: true,
                Message: "Hệ thống phát hiện bạn gặp khó khăn ở các câu hỏi liên tiếp (Quy tắc BR-03). Vui lòng hoàn thành gói ôn tập phụ đạo trước khi luyện tập tiếp."
            ));
        }

        if (progress.CurrentStep != "APPLY")
        {
            return Result<NextQuestionResponseDto>.Failure(
                Error.Validation("StageProgress.InvalidStep",
                    $"Tiến trình chặng học hiện đang ở bước '{progress.CurrentStep}'. Bạn cần hoàn thành bước LEARN (>= 80%) để mở khóa luyện tập APPLY."));
        }

        var answeredIds = progress.AdaptiveAttempts != null
            ? progress.AdaptiveAttempts.Select(a => a.QuestionId).ToHashSet()
            : new HashSet<Guid>();

        var candidatePool = new List<CandidateQuestion>();
        Guid skillId = progress.RoadmapNode?.SkillId ?? Guid.Empty;
        if (skillId == Guid.Empty)
        {
            _logger.LogWarning("StageProgress {ProgressId} không có RoadmapNode hoặc SkillId hợp lệ.", progress.Id);
        }

        try
        {
            // Pha APPLY: Luyện tập thích ứng ZPD + BKT theo dạng bài SkillId (truyền examId: null để ngân hàng câu hỏi thích ứng đa dạng theo đúng chuyên đề & môn học)
            // ReadOnly = true để KHÔNG tạo MockExam rác mỗi khi rút câu hỏi thích ứng!
            var quizResult = await _contentGrpcClient.GetMilestoneQuizReadOnlyAsync(skillId, examId: null, questionCount: 25, ct);
            if (quizResult != null && quizResult.Questions.Count > 0)
            {
                foreach (var q in quizResult.Questions)
                {
                    double diffB = MapDifficultyToParamB(q.DifficultyLevel);
                    double discA = 1.20;

                    candidatePool.Add(new CandidateQuestion(
                        q.QuestionId,
                        q.Content,
                        q.Options.Select(o => new CandidateQuestionOption(o.OptionId, o.Content)).ToList(),
                        q.DifficultyLevel,
                        diffB,
                        discA,
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
            _logger.LogWarning(ex, "Không thể kết nối Content Service gRPC để lấy ngân hàng câu hỏi ZPD.");
        }

        var selected = _zpdQuestionSelector.SelectNextQuestion(progress.BktMasteryPlt, candidatePool, answeredIds);

        if (selected == null)
        {
            // Nếu đã làm hết ngân hàng câu hỏi của chuyên đề và đạt mức nắm vững tốt (>= 0.70), tự động chuyển sang REFLECT
            if (progress.BktMasteryPlt >= 0.70 && progress.CurrentStep == "APPLY")
            {
                progress.CurrentStep = "REFLECT";
                progress.UpdatedAt = DateTime.UtcNow;
                await _stageProgressRepository.UpdateAsync(progress, ct);
            }

            return Result<NextQuestionResponseDto>.Success(new NextQuestionResponseDto(
                progress.Id,
                progress.CurrentStep,
                progress.Status,
                progress.BktMasteryPlt,
                currentAttemptCount,
                IsFinished: true,
                Message: progress.CurrentStep == "REFLECT"
                    ? "Bạn đã hoàn thành toàn bộ ngân hàng câu hỏi của chuyên đề với kết quả tốt! Hãy tiến hành bước REFLECT để tổng kết và mở khóa chặng kế tiếp."
                    : "Bạn đã làm hết các câu hỏi hiện có trong ngân hàng của chuyên đề này. Hãy ôn lại lý thuyết hoặc hoàn thành bước tổng kết."
            ));
        }

        var response = new NextQuestionResponseDto(
            progress.Id,
            progress.CurrentStep,
            progress.Status,
            progress.BktMasteryPlt,
            AttemptOrder: currentAttemptCount + 1,
            IsFinished: false,
            Message: "Đã chọn câu hỏi thích ứng tối ưu trong vùng phát triển gần nhất (ZPD).",
            selected.QuestionId,
            selected.Content,
            selected.Options.Select(o => new NextQuestionOptionDto(o.OptionId, o.Content)).ToList(),
            selected.DifficultyLevel,
            selected.ItemDifficultyB,
            selected.ItemDiscriminationA,
            selected.SkillId,
            selected.SkillName,
            selected.PassageTitle,
            selected.PassageContent,
            selected.PassageImageUrl
        );

        return Result<NextQuestionResponseDto>.Success(response);
    }

    internal static double MapDifficultyToParamB(int level) => level switch
    {
        1 => -1.0, // Nhận biết
        2 => -0.2, // Thông hiểu
        3 => 0.6,  // Vận dụng
        4 => 1.2,  // Vận dụng cao
        5 => 1.6,  // Phân tích / Tổng hợp
        6 => 2.0,  // Đánh giá / Sáng tạo
        _ => 0.0
    };
}
