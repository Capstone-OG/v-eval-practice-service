using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionsByStudent;

public record GetDiagnosticSubmissionsByStudentQuery(Guid StudentId)
    : IRequest<Result<List<DiagnosticSubmissionSummaryDto>>>;

public class GetDiagnosticSubmissionsByStudentHandler
    : IRequestHandler<GetDiagnosticSubmissionsByStudentQuery, Result<List<DiagnosticSubmissionSummaryDto>>>
{
    private readonly IExamSubmissionRepository _submissionRepository;

    public GetDiagnosticSubmissionsByStudentHandler(IExamSubmissionRepository submissionRepository)
    {
        _submissionRepository = submissionRepository;
    }

    public async Task<Result<List<DiagnosticSubmissionSummaryDto>>> Handle(
        GetDiagnosticSubmissionsByStudentQuery request,
        CancellationToken cancellationToken)
    {
        var submissions = await _submissionRepository.GetByStudentIdAsync(request.StudentId, cancellationToken);

        var dtos = submissions.Select(submission =>
        {
            double accuracy = submission.TotalQuestions > 0
                ? Math.Round((submission.TotalCorrect / (double)submission.TotalQuestions) * 100, 2)
                : 0;

            return new DiagnosticSubmissionSummaryDto
            {
                SubmissionId = submission.SubmissionId,
                StudentId = submission.StudentId,
                ExamId = submission.ExamId,
                ExamType = submission.ExamType,
                TotalScore = submission.TotalScore,
                TotalCorrect = submission.TotalCorrect,
                TotalQuestions = submission.TotalQuestions,
                AccuracyPercentage = accuracy,
                TotalTimeSpentSeconds = submission.TotalTimeSpentSeconds,
                StartedAt = submission.StartedAt,
                CompletedAt = submission.CompletedAt
            };
        }).ToList();

        return Result<List<DiagnosticSubmissionSummaryDto>>.Success(dtos);
    }
}
