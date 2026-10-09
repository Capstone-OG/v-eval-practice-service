using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Adaptive;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.SubmitDailyReview;

public class SubmitDailyReviewCommandHandler : IRequestHandler<SubmitDailyReviewCommand, Result<SubmitDailyReviewResponseDto>>
{
    private readonly IMistakeNotebookRepository _mistakeNotebookRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly ILogger<SubmitDailyReviewCommandHandler> _logger;

    public SubmitDailyReviewCommandHandler(
        IMistakeNotebookRepository mistakeNotebookRepository,
        IContentGrpcClient contentGrpcClient,
        ILogger<SubmitDailyReviewCommandHandler> logger)
    {
        _mistakeNotebookRepository = mistakeNotebookRepository;
        _contentGrpcClient = contentGrpcClient;
        _logger = logger;
    }

    public async Task<Result<SubmitDailyReviewResponseDto>> Handle(SubmitDailyReviewCommand request, CancellationToken ct)
    {
        var item = await _mistakeNotebookRepository.GetByIdAsync(request.MistakeNotebookId, ct);
        if (item == null)
        {
            return Result<SubmitDailyReviewResponseDto>.Failure(
                Error.NotFound("MistakeNotebook.NotFound", $"Không tìm thấy bản ghi Sổ tay lỗi sai {request.MistakeNotebookId}"));
        }

        string correctOpt = "A";
        string explanation = "Áp dụng định nghĩa và tính chất cơ sở của dạng bài để tìm ra đáp án đúng.";

        // 1. Tra cứu đáp án và lời giải thật từ Content Service nếu có
        try
        {
            var questionDetail = await _contentGrpcClient.GetQuestionDetailAsync(item.QuestionId, ct);
            if (questionDetail != null && questionDetail.Options.Count > 0)
            {
                var correctObj = questionDetail.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctObj != null)
                {
                    correctOpt = correctObj.OptionId;
                }

                if (!string.IsNullOrWhiteSpace(questionDetail.Explanation))
                {
                    explanation = questionDetail.Explanation;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy QuestionDetail từ Content Service qua gRPC.");
        }

        bool isCorrect = string.Equals(request.Request.SelectedOption?.Trim(), correctOpt, StringComparison.OrdinalIgnoreCase);

        // 2. Chạy thuật toán Lặp lại ngắt quãng SM-2
        var sm2Result = SpacedRepetitionCalculator.CalculateNextReview(
            isCorrect: isCorrect,
            currentConsecutiveCorrect: item.ConsecutiveCorrectReviews,
            currentIntervalDays: item.IntervalDays,
            currentEaseFactor: item.EaseFactor
        );

        // 3. Cập nhật dữ liệu tiến trình ôn tập
        item.ReviewCount++;
        item.ConsecutiveCorrectReviews = sm2Result.ConsecutiveCorrect;
        item.IntervalDays = sm2Result.IntervalDays;
        item.EaseFactor = sm2Result.EaseFactor;
        item.IsMastered = sm2Result.IsMastered;
        item.NextReviewDate = sm2Result.NextReviewDate;
        item.LastReviewedAt = DateTime.UtcNow;

        await _mistakeNotebookRepository.UpdateAsync(item, ct);

        _logger.LogInformation(
            "Học sinh {StudentId} nộp bài ôn tập câu {QuestionId}: Correct={IsCorrect}, Consecutive={Consecutive}, Mastered={IsMastered}, NextDate={NextDate:yyyy-MM-dd}",
            item.StudentId, item.QuestionId, isCorrect, item.ConsecutiveCorrectReviews, item.IsMastered, item.NextReviewDate);

        string message;
        if (sm2Result.IsMastered)
        {
            message = "Xuất sắc! Bạn đã làm đúng liên tiếp 3 lần dạng bài này. Lỗ hổng kiến thức đã được xóa sổ hoàn toàn (IsMastered = true)!";
        }
        else if (isCorrect)
        {
            message = $"Chính xác! Lần ôn tập tiếp theo sẽ được lùi lại sau {sm2Result.IntervalDays} ngày ({sm2Result.NextReviewDate:dd/MM/yyyy}) theo quy luật củng cố trí nhớ dài hạn SM-2.";
        }
        else
        {
            message = $"Chưa chính xác. Đáp án đúng là {correctOpt}. Hệ thống đã lên lịch ôn lại vào ngày mai ({sm2Result.NextReviewDate:dd/MM/yyyy}) để bạn kịp thời củng cố.";
        }

        var response = new SubmitDailyReviewResponseDto(
            MistakeNotebookId: item.Id,
            IsCorrect: isCorrect,
            CorrectOption: correctOpt,
            Explanation: explanation,
            ConsecutiveCorrectReviews: item.ConsecutiveCorrectReviews,
            IntervalDays: item.IntervalDays,
            EaseFactor: item.EaseFactor,
            IsMastered: item.IsMastered,
            NextReviewDate: item.NextReviewDate,
            Message: message
        );

        return Result<SubmitDailyReviewResponseDto>.Success(response);
    }
}
