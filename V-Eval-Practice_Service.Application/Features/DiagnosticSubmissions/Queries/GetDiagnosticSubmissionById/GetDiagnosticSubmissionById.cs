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
using V_Eval_Practice_Service.Domain.Constants;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Queries.GetDiagnosticSubmissionById;

public record GetDiagnosticSubmissionByIdQuery(Guid SubmissionId)
    : IRequest<Result<SubmitDiagnosticResponseDto>>;

public class GetDiagnosticSubmissionByIdHandler
    : IRequestHandler<GetDiagnosticSubmissionByIdQuery, Result<SubmitDiagnosticResponseDto>>
{
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IContentGrpcClient _contentGrpcClient;

    public GetDiagnosticSubmissionByIdHandler(
        IExamSubmissionRepository submissionRepository,
        IContentGrpcClient contentGrpcClient)
    {
        _submissionRepository = submissionRepository;
        _contentGrpcClient = contentGrpcClient;
    }

    public async Task<Result<SubmitDiagnosticResponseDto>> Handle(
        GetDiagnosticSubmissionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null)
        {
            return Result<SubmitDiagnosticResponseDto>.Failure(
                Error.NotFound("Submission.NotFound", $"Không tìm thấy bài nộp có ID {request.SubmissionId}."));
        }

        // Lấy bảng đáp án từ Content Service để làm giàu dữ liệu chi tiết
        IReadOnlyDictionary<Guid, ExamQuestionKeyDto> answerKeys;
        try
        {
            answerKeys = await _contentGrpcClient.GetExamAnswerKeysAsync(submission.ExamId, cancellationToken);
        }
        catch
        {
            answerKeys = new Dictionary<Guid, ExamQuestionKeyDto>();
        }

        var orderedAnswers = submission.Answers.ToList();
        var questionResults = orderedAnswers
            .Select((a, idx) =>
            {
                answerKeys.TryGetValue(a.QuestionId, out var key);
                int qOrder = key?.QuestionOrder ?? (idx + 1);

                string skillId = key?.SkillId ?? string.Empty;
                string skillName = key?.SkillName ?? string.Empty;
                string domainId = key?.DomainId ?? string.Empty;
                string domainName = key?.DomainName ?? string.Empty;
                int diffLevel = key?.DifficultyLevel ?? 0;
                string correctOpt = key?.CorrectOption ?? string.Empty;

                return new QuestionResultDto
                {
                    QuestionId = a.QuestionId,
                    QuestionOrder = qOrder,
                    SelectedOption = a.SelectedOption,
                    CorrectOption = correctOpt,
                    IsCorrect = a.IsCorrect,
                    TimeSpentSeconds = a.TimeSpentSeconds,
                    SkillId = skillId,
                    SkillName = skillName,
                    DomainId = domainId,
                    DomainName = domainName,
                    DifficultyLevel = diffLevel
                };
            })
            .OrderBy(q => q.QuestionOrder)
            .ToList();

        // Phân tích Kỹ năng
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

        var weakSkillIds = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => s.SkillId)
            .ToList();

        var weakSkills = skillBreakdowns
            .Where(s => s.IsWeak)
            .Select(s => new WeakSkillDto
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                DomainName = s.DomainName,
                TotalQuestions = s.TotalQuestions,
                CorrectCount = s.CorrectCount,
                AccuracyPercentage = s.AccuracyPercentage
            })
            .ToList();

        // Phân tích Độ khó
        var difficultyBreakdowns = questionResults
            .Where(q => q.DifficultyLevel > 0)
            .GroupBy(q => q.DifficultyLevel)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int count = g.Count();
                int correct = g.Count(x => x.IsCorrect);
                double pct = Math.Round((correct / (double)count) * 100, 2);
                string levelName = BloomTaxonomy.GetName(g.Key);

                return new DifficultyBreakdownDto
                {
                    DifficultyLevel = g.Key,
                    LevelName = levelName,
                    TotalQuestions = count,
                    CorrectCount = correct,
                    AccuracyPercentage = pct
                };
            })
            .ToList();

        double accuracy = submission.TotalQuestions > 0
            ? Math.Round((submission.TotalCorrect / (double)submission.TotalQuestions) * 100, 2)
            : 0;

        // Xây dựng RadarChart và SkillPriors đầy đủ cho màn hình Lịch sử bài làm
        var radarChart = BuildRadarChart(questionResults, accuracy);
        var skillPriors = BuildSkillPriors(skillBreakdowns, submission.Theta0 ?? 0.0);

        var dto = new SubmitDiagnosticResponseDto
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
            StartedAt = submission.StartedAt,
            CompletedAt = submission.CompletedAt,
            SkillBreakdowns = skillBreakdowns,
            WeakSkillIds = weakSkillIds,
            WeakSkills = weakSkills,
            DifficultyBreakdowns = difficultyBreakdowns,
            Questions = questionResults,
            Theta0 = submission.Theta0 ?? 0.0,
            PlacementClass = submission.PlacementClass ?? "ACCELERATION",
            ClassId = submission.EnrolledClassId,
            AiCommentary = submission.AiCommentary ?? string.Empty,
            RadarChart = radarChart,
            SkillPriors = skillPriors
        };

        return Result<SubmitDiagnosticResponseDto>.Success(dto);
    }

    public static List<DiagnosticRadarAxisDto> BuildRadarChart(
        IReadOnlyList<QuestionResultDto> questionResults,
        double fallbackAccuracyPct = 0.0)
    {
        return questionResults
            .Where(q => !string.IsNullOrWhiteSpace(q.DomainId) || !string.IsNullOrWhiteSpace(q.DomainName))
            .GroupBy(q => NormalizeDomainCode(q.DomainId, q.DomainName))
            .Select(g =>
            {
                var first = g.First();
                string domainId = !string.IsNullOrWhiteSpace(first.DomainId) ? first.DomainId : g.Key;
                string domainName = !string.IsNullOrWhiteSpace(first.DomainName)
                    ? first.DomainName
                    : GetDefaultDomainName(g.Key);
                double studentPct = Math.Round((g.Count(x => x.IsCorrect) / (double)Math.Max(1, g.Count())) * 100.0, 1);

                return new DiagnosticRadarAxisDto
                {
                    DomainId = domainId,
                    DomainName = domainName,
                    StudentPct = studentPct,
                    BenchmarkPct = 80.0
                };
            })
            .ToList();
    }

    public static List<DiagnosticSkillPriorDto> BuildSkillPriors(
        IReadOnlyList<SkillDiagnosticDto> skillBreakdowns,
        double fallbackTheta0 = 0.0)
    {
        if (skillBreakdowns.Count == 0)
        {
            return new List<DiagnosticSkillPriorDto>();
        }

        return skillBreakdowns.Select(sb =>
        {
            double pl0 = Math.Round(Math.Clamp(sb.AccuracyPercentage / 100.0, 0.05, 0.98), 4);
            double thetaSkill = Math.Round(Math.Log(pl0 / (1.0 - pl0)), 2);
            string domCode = NormalizeDomainCode(sb.DomainId, sb.DomainName);
            return new DiagnosticSkillPriorDto
            {
                SkillId = sb.SkillId,
                SkillName = !string.IsNullOrWhiteSpace(sb.SkillName) ? sb.SkillName : sb.SkillId,
                DomainId = !string.IsNullOrWhiteSpace(sb.DomainId) ? sb.DomainId : domCode,
                DomainName = !string.IsNullOrWhiteSpace(sb.DomainName) ? sb.DomainName : GetDefaultDomainName(domCode),
                ThetaSkill = thetaSkill,
                PL0 = pl0,
                Source = "measured"
            };
        }).ToList();
    }

    internal static string NormalizeDomainCode(string? domainId, string? domainName)
    {
        string raw = $"{domainId} {domainName}".ToLowerInvariant();
        if (raw.Contains("dom_lang") || raw.Contains("ngôn ngữ") || raw.Contains("tiếng việt") || raw.Contains("english") || raw.Contains("văn"))
        {
            return "DOM_LANG";
        }
        if (raw.Contains("dom_nat") || raw.Contains("tự nhiên") || raw.Contains("vật lý") || raw.Contains("hóa") || raw.Contains("sinh"))
        {
            return "DOM_NAT_SCI";
        }
        if (raw.Contains("dom_soc") || raw.Contains("xã hội") || raw.Contains("lịch sử") || raw.Contains("địa lý"))
        {
            return "DOM_SOC_SCI";
        }
        return "DOM_MATH";
    }

    internal static string GetDefaultDomainName(string domainCode) => domainCode switch
    {
        "DOM_LANG" => "Sử dụng Ngôn ngữ (Việt - Anh)",
        "DOM_NAT_SCI" => "Giải quyết vấn đề - Khoa học Tự nhiên",
        "DOM_SOC_SCI" => "Giải quyết vấn đề - Khoa học Xã hội",
        _ => "Toán học, Tư duy Logic & Phân tích số liệu"
    };
}
