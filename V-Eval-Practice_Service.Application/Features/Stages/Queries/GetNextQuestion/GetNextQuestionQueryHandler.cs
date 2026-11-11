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
        // 1. Kiểm tra tồn tại của StageProgress
        var progress = await _stageProgressRepository.GetByIdAsync(request.StageProgressId, ct);
        if (progress == null)
        {
            return Result<NextQuestionResponseDto>.Failure(
                Error.NotFound("StageProgress.NotFound", $"Không tìm thấy tiến trình chặng học {request.StageProgressId}"));
        }

        int currentAttemptCount = progress.AdaptiveAttempts?.Count ?? 0;

        // 2. Kiểm tra nếu đã hoàn thành chặng hoặc đã chuyển sang REFLECT
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

        // 3. Kiểm tra nếu đang bị chặn do kích hoạt quy tắc phụ đạo BR-03
        if (progress.Status == "REMEDIAL_REQUIRED")
        {
            return Result<NextQuestionResponseDto>.Success(new NextQuestionResponseDto(
                progress.Id,
                progress.CurrentStep,
                progress.Status,
                progress.BktMasteryPlt,
                currentAttemptCount,
                IsFinished: true,
                Message: "Hệ thống phát hiện bạn gặp khó khăn ở các câu hỏi liên tiếp (Quy tắc BR-03). Vui lòng xem clip ôn tập phụ đạo trước khi luyện tập tiếp."
            ));
        }

        // 4. Kiểm tra điều kiện máy trạng thái: Phải đang ở bước APPLY
        if (progress.CurrentStep != "APPLY")
        {
            return Result<NextQuestionResponseDto>.Failure(
                Error.Validation("StageProgress.InvalidStep",
                    $"Tiến trình chặng học hiện đang ở bước '{progress.CurrentStep}'. Bạn cần hoàn thành các bước trước để mở khóa luyện tập APPLY."));
        }

        // 5. Thu thập danh sách ID câu hỏi đã trả lời trong phiên này
        var answeredIds = progress.AdaptiveAttempts != null
            ? progress.AdaptiveAttempts.Select(a => a.QuestionId).ToHashSet()
            : new HashSet<Guid>();

        // 6. Nạp ngân hàng câu hỏi ứng viên (Candidate Question Pool) từ Content Service hoặc Fallback
        var candidatePool = new List<CandidateQuestion>();
        Guid skillId = progress.RoadmapNode?.SkillId ?? Guid.Parse("6f3765db-943e-4810-bf74-d6a8bdc215da");
        Guid? examId = progress.RoadmapNode?.QuizExamId;

        try
        {
            var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(skillId, examId, questionCount: 15, ct);
            if (quizResult != null && quizResult.Questions.Count > 0)
            {
                foreach (var q in quizResult.Questions)
                {
                    double diffB = MapDifficultyToParamB(q.DifficultyLevel);
                    double discA = 1.20; // Độ phân biệt chuẩn hóa

                    candidatePool.Add(new CandidateQuestion(
                        q.QuestionId,
                        q.Content,
                        q.Options.Select(o => new CandidateQuestionOption(o.OptionId, o.Content)).ToList(),
                        q.DifficultyLevel,
                        diffB,
                        discA,
                        q.SkillId,
                        q.SkillName
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kết nối Content Service gRPC để lấy ngân hàng câu hỏi. Kích hoạt fallback an toàn.");
        }

        // Nếu pool rỗng (ví dụ môi trường test offline), nạp pool câu hỏi thích ứng mẫu đa cấp độ
        if (candidatePool.Count == 0)
        {
            candidatePool.AddRange(GetSampleAdaptiveQuestionPool(skillId));
        }

        // 7. Gọi thuật toán IRT 2PL lọc câu hỏi tối ưu trong vùng ZPD [0.60, 0.75]
        var selected = _zpdQuestionSelector.SelectNextQuestion(progress.BktMasteryPlt, candidatePool, answeredIds);

        if (selected == null)
        {
            // Nếu đã làm hết câu hỏi trong ngân hàng mà chưa đạt BR-01
            return Result<NextQuestionResponseDto>.Success(new NextQuestionResponseDto(
                progress.Id,
                progress.CurrentStep,
                progress.Status,
                progress.BktMasteryPlt,
                currentAttemptCount,
                IsFinished: true,
                Message: "Bạn đã hoàn thành toàn bộ câu hỏi trong ngân hàng luyện tập của chặng này."
            ));
        }

        var response = new NextQuestionResponseDto(
            progress.Id,
            progress.CurrentStep,
            progress.Status,
            progress.BktMasteryPlt,
            AttemptOrder: currentAttemptCount + 1,
            IsFinished: false,
            Message: "Đã chọn câu hỏi thích ứng tối ưu trong vùng ZPD.",
            selected.QuestionId,
            selected.Content,
            selected.Options.Select(o => new NextQuestionOptionDto(o.OptionId, o.Content)).ToList(),
            selected.DifficultyLevel,
            selected.ItemDifficultyB,
            selected.ItemDiscriminationA,
            selected.SkillId,
            selected.SkillName
        );

        return Result<NextQuestionResponseDto>.Success(response);
    }

    private static double MapDifficultyToParamB(int level) => level switch
    {
        1 => -1.0, // Nhận biết
        2 => -0.2, // Thông hiểu
        3 => 0.6,  // Vận dụng
        4 => 1.2,  // Vận dụng cao
        _ => 0.0
    };

    private static List<CandidateQuestion> GetSampleAdaptiveQuestionPool(Guid skillId)
    {
        return new List<CandidateQuestion>
        {
            new(
                Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                "Tìm đạo hàm của hàm số y = x^3 - 3x + 2.",
                new List<CandidateQuestionOption>
                {
                    new("A", "y' = 3x^2 - 3"),
                    new("B", "y' = 3x^2 - 3x"),
                    new("C", "y' = x^2 - 3"),
                    new("D", "y' = 3x^2 + 2")
                },
                1, -1.0, 1.1, skillId, "Đạo hàm cơ bản"
            ),
            new(
                Guid.Parse("a2222222-2222-2222-2222-222222222222"),
                "Cho hàm số y = (2x + 1)/(x - 1). Tính hệ số góc tiếp tuyến của đồ thị hàm số tại x = 2.",
                new List<CandidateQuestionOption>
                {
                    new("A", "k = -3"),
                    new("B", "k = 3"),
                    new("C", "k = -1"),
                    new("D", "k = 5")
                },
                2, -0.2, 1.2, skillId, "Tiếp tuyến đồ thị"
            ),
            new(
                Guid.Parse("a3333333-3333-3333-3333-333333333333"),
                "Tìm tất cả các giá trị của tham số m để hàm số y = x^3 - 3mx^2 + 3(m^2 - 1)x đạt cực đại tại x = 1.",
                new List<CandidateQuestionOption>
                {
                    new("A", "m = 0"),
                    new("B", "m = 2"),
                    new("C", "m = 1"),
                    new("D", "m = -1")
                },
                3, 0.6, 1.35, skillId, "Cực trị chứa tham số m"
            ),
            new(
                Guid.Parse("a4444444-4444-4444-4444-444444444444"),
                "Cho hàm số f(x) liên tục trên R và f'(x) = (x - 1)^2(x + 2)(x - 3). Số điểm cực trị của hàm số là:",
                new List<CandidateQuestionOption>
                {
                    new("A", "2"),
                    new("B", "3"),
                    new("C", "1"),
                    new("D", "4")
                },
                3, 0.7, 1.4, skillId, "Điểm cực trị hàm hợp"
            ),
            new(
                Guid.Parse("a5555555-5555-5555-5555-555555555555"),
                "Tìm m để bất phương trình f(x) >= m nghiệm đúng với mọi x thuộc [0; 3] biết max f(x) = 10.",
                new List<CandidateQuestionOption>
                {
                    new("A", "m <= 2"),
                    new("B", "m <= min f(x)"),
                    new("C", "m >= 10"),
                    new("D", "m < 0")
                },
                4, 1.25, 1.5, skillId, "Vận dụng cao bất đẳng thức hàm số"
            )
        };
    }
}
