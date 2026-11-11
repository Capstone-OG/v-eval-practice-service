using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetMockExamDetail;

public class GetMockExamDetailQueryHandler : IRequestHandler<GetMockExamDetailQuery, Result<GetMockExamDetailResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly ILogger<GetMockExamDetailQueryHandler> _logger;

    public GetMockExamDetailQueryHandler(
        IMockExamRepository mockExamRepository,
        ILogger<GetMockExamDetailQueryHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _logger = logger;
    }

    public async Task<Result<GetMockExamDetailResponseDto>> Handle(
        GetMockExamDetailQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Truy vấn thông tin chi tiết phiên thi thử và hồ sơ giám sát {SubmissionId}", request.SubmissionId);

        var submission = await _mockExamRepository.GetByIdWithDetailsAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<GetMockExamDetailResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy phiên làm bài thi thử với ID {request.SubmissionId}."));
        }

        var snapshotDtos = (submission.ProctoringSnapshots ?? Enumerable.Empty<Domain.Entities.ProctoringSnapshot>())
            .OrderBy(p => p.CapturedAt)
            .Select(p => new MockExamSnapshotDto(
                p.Id,
                p.SnapshotUrl,
                p.CapturedAt,
                p.FaceDetected
            ))
            .ToList();

        var answerDtos = (submission.Answers ?? Enumerable.Empty<Domain.Entities.SubmissionAnswer>())
            .Select(a => new MockExamAnswerDetailDto(
                a.QuestionId,
                a.SelectedOption,
                a.IsCorrect,
                a.TimeSpentSeconds
            ))
            .ToList();

        var response = new GetMockExamDetailResponseDto(
            submission.SubmissionId,
            submission.ExamId,
            submission.StudentId,
            submission.ExamType,
            submission.TotalScore,
            submission.TotalCorrect,
            submission.TotalQuestions,
            submission.TotalTimeSpentSeconds,
            submission.StartedAt,
            submission.CompletedAt,
            submission.Status,
            submission.TabDepartureCount,
            submission.IntegrityStatus,
            submission.MentorVerdict,
            submission.MentorNotes,
            snapshotDtos,
            answerDtos
        );

        return Result<GetMockExamDetailResponseDto>.Success(response);
    }
}
