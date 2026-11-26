using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Queries.GetMockExamDetail;

public class GetMockExamDetailQueryHandler : IRequestHandler<GetMockExamDetailQuery, Result<GetMockExamDetailResponseDto>>
{
    private readonly IMockExamRepository _mockExamRepository;
    private readonly IContentGrpcClient _contentGrpcClient;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly ILogger<GetMockExamDetailQueryHandler> _logger;

    public GetMockExamDetailQueryHandler(
        IMockExamRepository mockExamRepository,
        IContentGrpcClient contentGrpcClient,
        IIdentityGrpcClient identityGrpcClient,
        ILogger<GetMockExamDetailQueryHandler> logger)
    {
        _mockExamRepository = mockExamRepository;
        _contentGrpcClient = contentGrpcClient;
        _identityGrpcClient = identityGrpcClient;
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

        IReadOnlyDictionary<Guid, ExamQuestionKeyDto>? answerKeys = null;
        if (submission.ExamId != Guid.Empty)
        {
            try
            {
                answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể nạp chi tiết đáp án đề thi thử {ExamId} từ Content Service.", submission.ExamId);
            }
        }

        var answerDtos = (submission.Answers ?? Enumerable.Empty<Domain.Entities.SubmissionAnswer>())
            .Select((a, idx) =>
            {
                ExamQuestionKeyDto? keyInfo = null;
                answerKeys?.TryGetValue(a.QuestionId, out keyInfo);
                return new MockExamAnswerDetailDto(
                    QuestionId: a.QuestionId,
                    SelectedOption: a.SelectedOption,
                    IsCorrect: a.IsCorrect,
                    TimeSpentSeconds: a.TimeSpentSeconds,
                    CorrectOption: keyInfo?.CorrectOption,
                    QuestionOrder: keyInfo?.QuestionOrder > 0 ? keyInfo.QuestionOrder : idx + 1,
                    Content: keyInfo?.Content,
                    Options: keyInfo?.Options,
                    Explanation: keyInfo?.Explanation,
                    SkillName: keyInfo?.SkillName
                );
            })
            .OrderBy(a => a.QuestionOrder)
            .ToList();

        string studentName = string.Empty;
        if (submission.StudentId != Guid.Empty)
        {
            try
            {
                var names = await _identityGrpcClient.GetStudentNamesAsync(new[] { submission.StudentId }, cancellationToken);
                if (names.TryGetValue(submission.StudentId, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
                {
                    studentName = resolved;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tra cứu họ tên thí sinh {StudentId} từ Identity Service.", submission.StudentId);
            }
        }

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
            answerDtos,
            studentName
        );

        return Result<GetMockExamDetailResponseDto>.Success(response);
    }
}
