using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.MentorReview;

public class MentorReviewCommandHandler : IRequestHandler<MentorReviewCommand, Result<MentorReviewResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly ILogger<MentorReviewCommandHandler> _logger;

    public MentorReviewCommandHandler(
        IMockExamRepository mockExamRepository,
        ILogger<MentorReviewCommandHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _logger = logger;
    }

    public async Task<Result<MentorReviewResponseDto>> Handle(
        MentorReviewCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Mentor tiến hành thẩm định bài thi {SubmissionId} với kết luận {Verdict}",
            request.SubmissionId, request.Verdict);

        var submission = await _mockExamRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<MentorReviewResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy phiên làm bài thi thử với ID {request.SubmissionId}."));
        }

        if (submission.Status == "IN_PROGRESS")
        {
            return Result<MentorReviewResponseDto>.Failure(
                Error.Conflict("Submission.InProgress",
                    "Bài thi đang trong thời gian thí sinh làm bài (IN_PROGRESS), chưa thể thực hiện thẩm định kết quả."));
        }

        submission.MentorVerdict = request.Verdict;
        submission.MentorNotes = request.Notes;

        string message;
        if (request.Verdict == "APPROVED")
        {
            submission.Status = "COMPLETED";
            submission.IntegrityStatus = "NORMAL";
            message = $"Bài thi {submission.SubmissionId} đã được Mentor phê duyệt hợp lệ. Điểm số {submission.TotalScore} được công nhận chính thức.";
        }
        else // DISQUALIFIED
        {
            submission.Status = "DISQUALIFIED";
            submission.TotalScore = 0;
            submission.TotalCorrect = 0;
            message = $"Bài thi {submission.SubmissionId} đã bị Mentor hủy kết quả (DISQUALIFIED) do vi phạm quy chế thi. Điểm số được đưa về 0.";
        }

        await _mockExamRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Thẩm định hoàn tất cho bài thi {SubmissionId}: Verdict={Verdict}, Status={Status}",
            submission.SubmissionId, submission.MentorVerdict, submission.Status);

        var response = new MentorReviewResponseDto(
            submission.SubmissionId,
            submission.ExamId,
            submission.StudentId,
            submission.TotalScore,
            submission.Status,
            submission.IntegrityStatus,
            submission.MentorVerdict,
            submission.MentorNotes,
            DateTime.UtcNow,
            message
        );

        return Result<MentorReviewResponseDto>.Success(response);
    }
}
