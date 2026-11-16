using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionsByStudent;

public record GetDiagnosticSubmissionsByStudentQuery(Guid StudentId)
    : IRequest<Result<List<DiagnosticSubmissionSummaryDto>>>;

public class GetDiagnosticSubmissionsByStudentHandler
    : IRequestHandler<GetDiagnosticSubmissionsByStudentQuery, Result<List<DiagnosticSubmissionSummaryDto>>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IContentGrpcClient? _contentGrpcClient;

    public GetDiagnosticSubmissionsByStudentHandler(
        IExamSubmissionRepository submissionRepository,
        IContentGrpcClient? contentGrpcClient = null)
    {
        _submissionRepository = submissionRepository;
        _contentGrpcClient = contentGrpcClient;
    }

    public async Task<Result<List<DiagnosticSubmissionSummaryDto>>> Handle(
        GetDiagnosticSubmissionsByStudentQuery request,
        CancellationToken cancellationToken)
    {
        var submissions = await _submissionRepository.GetByStudentIdAsync(request.StudentId, cancellationToken);
        var answerKeyCache = new Dictionary<Guid, IReadOnlyDictionary<Guid, ExamQuestionKeyDto>>();

        var dtos = new List<DiagnosticSubmissionSummaryDto>();
        foreach (var submission in submissions)
        {
            double accuracy = submission.TotalQuestions > 0
                ? Math.Round((submission.TotalCorrect / (double)submission.TotalQuestions) * 100, 2)
                : 0;

            IReadOnlyDictionary<Guid, ExamQuestionKeyDto> answerKeys = new Dictionary<Guid, ExamQuestionKeyDto>();
            if (_contentGrpcClient != null && submission.ExamId != Guid.Empty)
            {
                if (!answerKeyCache.TryGetValue(submission.ExamId, out var cachedKeys))
                {
                    try
                    {
                        cachedKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, cancellationToken);
                    }
                    catch
                    {
                        cachedKeys = new Dictionary<Guid, ExamQuestionKeyDto>();
                    }
                    answerKeyCache[submission.ExamId] = cachedKeys;
                }
                answerKeys = cachedKeys;
            }

            var questionResults = submission.Answers
                .Select((a, idx) =>
                {
                    answerKeys.TryGetValue(a.QuestionId, out var key);
                    int qOrder = key?.QuestionOrder ?? (idx + 1);

                    return new QuestionResultDto
                    {
                        QuestionId = a.QuestionId,
                        QuestionOrder = qOrder,
                        SelectedOption = a.SelectedOption,
                        CorrectOption = key?.CorrectOption ?? string.Empty,
                        IsCorrect = a.IsCorrect,
                        TimeSpentSeconds = a.TimeSpentSeconds,
                        SkillId = key?.SkillId ?? string.Empty,
                        SkillName = key?.SkillName ?? string.Empty,
                        DomainId = key?.DomainId ?? string.Empty,
                        DomainName = key?.DomainName ?? string.Empty,
                        DifficultyLevel = key?.DifficultyLevel ?? 0
                    };
                })
                .OrderBy(q => q.QuestionOrder)
                .ToList();

            var skillBreakdowns = questionResults
                .Where(q => !string.IsNullOrEmpty(q.SkillId))
                .GroupBy(q => q.SkillId)
                .Select(g =>
                {
                    var first = g.First();
                    int count = g.Count();
                    int correct = g.Count(x => x.IsCorrect);
                    double pct = Math.Round((correct / (double)count) * 100, 2);
                    return new SkillDiagnosticDto
                    {
                        SkillId = g.Key,
                        SkillName = first.SkillName,
                        DomainId = first.DomainId,
                        DomainName = first.DomainName,
                        TotalQuestions = count,
                        CorrectCount = correct,
                        AccuracyPercentage = pct,
                        IsWeak = pct < 60.0
                    };
                })
                .ToList();

            double theta0 = submission.Theta0 ?? 0.0;
            var radarChart = GetDiagnosticSubmissionByIdHandler.BuildRadarChart(questionResults, accuracy);
            var skillPriors = GetDiagnosticSubmissionByIdHandler.BuildSkillPriors(skillBreakdowns, theta0);

            dtos.Add(new DiagnosticSubmissionSummaryDto
            {
                SubmissionId = submission.SubmissionId,
                StudentId = submission.StudentId,
                ExamId = submission.ExamId,
                ExamType = submission.ExamType,
                Status = submission.Status,
                TotalScore = submission.TotalScore,
                TotalCorrect = submission.TotalCorrect,
                TotalQuestions = submission.TotalQuestions,
                AccuracyPercentage = accuracy,
                TotalTimeSpentSeconds = submission.TotalTimeSpentSeconds,
                Theta0 = theta0,
                PlacementClass = submission.PlacementClass ?? "ACCELERATION",
                StartedAt = submission.StartedAt,
                CompletedAt = submission.CompletedAt,
                RadarChart = radarChart,
                SkillPriors = skillPriors
            });
        }

        return Result<List<DiagnosticSubmissionSummaryDto>>.Success(dtos);
    }
}
