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
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetDailyReview;

public class GetDailyReviewQueryHandler : IRequestHandler<GetDailyReviewQuery, Result<DailyReviewListResponseDto>>
{
    private readonly IMistakeNotebookRepository _mistakeNotebookRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<GetDailyReviewQueryHandler> _logger;

    public GetDailyReviewQueryHandler(
        IMistakeNotebookRepository mistakeNotebookRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<GetDailyReviewQueryHandler> logger)
    {
        _mistakeNotebookRepository = mistakeNotebookRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<DailyReviewListResponseDto>> Handle(GetDailyReviewQuery request, CancellationToken ct)
    {
        var targetDate = DateTime.SpecifyKind((request.TargetDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc);

        var dueItems = await _mistakeNotebookRepository.GetDailyReviewItemsAsync(
            request.StudentId, targetDate, ct);

        var questions = new List<DailyReviewQuestionDto>();

        foreach (var item in dueItems)
        {
            DailyReviewQuestionDto? reviewQ = null;

            // 1. Cố gắng tìm câu hỏi biến thể (Isomorphic Variant) cùng SkillId từ Ngân hàng đề Content Service
            if (item.SkillId != Guid.Empty)
            {
                try
                {
                    var quizResult = await _contentGrpcClient.GetMilestoneQuizAsync(item.SkillId, null, questionCount: 5, ct);
                    if (quizResult != null && quizResult.Questions.Count > 0)
                    {
                        // Chọn câu hỏi khác với câu đã làm sai ban đầu (Isomorphic variant)
                        var variant = quizResult.Questions.FirstOrDefault(q => q.QuestionId != item.QuestionId) 
                                      ?? quizResult.Questions.First();

                        reviewQ = new DailyReviewQuestionDto(
                            MistakeNotebookId: item.Id,
                            OriginalQuestionId: item.QuestionId,
                            ReviewQuestionId: variant.QuestionId,
                            SkillId: item.SkillId,
                            SkillName: variant.SkillName ?? "Chuyên đề trọng tâm",
                            PatternId: item.PatternId,
                            Content: variant.Content,
                            Options: variant.Options.Select(o => new DailyReviewOptionDto(o.OptionId, o.Content)).ToList(),
                            DifficultyLevel: variant.DifficultyLevel,
                            ReviewCount: item.ReviewCount,
                            ConsecutiveCorrect: item.ConsecutiveCorrectReviews,
                            CognitiveErrorTag: item.CognitiveErrorTag,
                            StudentNotes: item.StudentNotes,
                            IsIsomorphicVariant: variant.QuestionId != item.QuestionId
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể lấy câu hỏi biến thể từ Content Service cho SkillId {SkillId}", item.SkillId);
                }
            }

            // 2. Fallback: Nếu không kết nối được Content Service, sinh câu hỏi biến thể củng cố an toàn
            if (reviewQ == null)
            {
                reviewQ = new DailyReviewQuestionDto(
                    MistakeNotebookId: item.Id,
                    OriginalQuestionId: item.QuestionId,
                    ReviewQuestionId: item.QuestionId,
                    SkillId: item.SkillId,
                    SkillName: "Chuyên đề trọng tâm",
                    PatternId: item.PatternId,
                    Content: $"[Câu hỏi biến thể ôn tập] Kiểm tra và củng cố kiến thức cho dạng bài: {item.PatternId}. Phương án nào dưới đây mô tả chính xác phương pháp suy luận chuẩn?",
                    Options: new List<DailyReviewOptionDto>
                    {
                        new("A", "Áp dụng định nghĩa và định lý cơ sở theo tuần tự các bước"),
                        new("B", "Phán đoán trực giác bỏ qua các bước biến đổi trung gian"),
                        new("C", "Bỏ qua điều kiện xác định và tập nghiệm"),
                        new("D", "Chọn phương án ngẫu nhiên không qua kiểm tra")
                    },
                    DifficultyLevel: 2,
                    ReviewCount: item.ReviewCount,
                    ConsecutiveCorrect: item.ConsecutiveCorrectReviews,
                    CognitiveErrorTag: item.CognitiveErrorTag,
                    StudentNotes: item.StudentNotes,
                    IsIsomorphicVariant: false
                );
            }

            questions.Add(reviewQ);
        }

        var response = new DailyReviewListResponseDto(
            StudentId: request.StudentId,
            ReviewDate: targetDate,
            TotalDueQuestions: questions.Count,
            Questions: questions
        );

        return Result<DailyReviewListResponseDto>.Success(response);
    }
}
