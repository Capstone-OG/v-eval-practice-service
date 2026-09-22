using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces;

public record DiagnosticAnswerItemPayload(
    [property: JsonPropertyName("question_id")] string QuestionId,
    [property: JsonPropertyName("skill_id")] string SkillId,
    [property: JsonPropertyName("domain_id")] string DomainId,
    [property: JsonPropertyName("difficulty_level")] int DifficultyLevel,
    [property: JsonPropertyName("is_correct")] bool IsCorrect,
    [property: JsonPropertyName("time_spent_seconds")] int TimeSpentSeconds
);

public record DiagnosticDomainNamePayload(
    [property: JsonPropertyName("domain_id")] string DomainId,
    [property: JsonPropertyName("domain_name")] string DomainName
);

public record DiagnosticAnalyzeRequestPayload(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("submission_id")] string SubmissionId,
    [property: JsonPropertyName("target_score")] int TargetScore,
    [property: JsonPropertyName("answers")] List<DiagnosticAnswerItemPayload> Answers,
    [property: JsonPropertyName("domain_names")] List<DiagnosticDomainNamePayload> DomainNames,
    [property: JsonPropertyName("all_skill_ids")] Dictionary<string, string> AllSkillIds
);

public record DiagnosticSkillPriorResult(
    [property: JsonPropertyName("skill_id")] string SkillId,
    [property: JsonPropertyName("domain_id")] string DomainId,
    [property: JsonPropertyName("theta_skill")] double ThetaSkill,
    [property: JsonPropertyName("p_l0")] double PL0,
    [property: JsonPropertyName("source")] string Source
);

public record DiagnosticDomainScoreResult(
    [property: JsonPropertyName("domain_id")] string DomainId,
    [property: JsonPropertyName("domain_name")] string DomainName,
    [property: JsonPropertyName("theta_domain")] double ThetaDomain,
    [property: JsonPropertyName("total_questions")] int TotalQuestions,
    [property: JsonPropertyName("correct_count")] int CorrectCount,
    [property: JsonPropertyName("accuracy_pct")] double AccuracyPct
);

public record DiagnosticRadarAxisResult(
    [property: JsonPropertyName("domain_id")] string DomainId,
    [property: JsonPropertyName("domain_name")] string DomainName,
    [property: JsonPropertyName("student_pct")] double StudentPct,
    [property: JsonPropertyName("benchmark_pct")] double BenchmarkPct
);

public record DiagnosticAnalyzeResponsePayload(
    [property: JsonPropertyName("student_id")] string StudentId,
    [property: JsonPropertyName("submission_id")] string SubmissionId,
    [property: JsonPropertyName("theta_0")] double Theta0,
    [property: JsonPropertyName("placement_class")] string PlacementClass,
    [property: JsonPropertyName("domain_scores")] List<DiagnosticDomainScoreResult> DomainScores,
    [property: JsonPropertyName("skill_priors")] List<DiagnosticSkillPriorResult> SkillPriors,
    [property: JsonPropertyName("radar_chart")] List<DiagnosticRadarAxisResult> RadarChart,
    [property: JsonPropertyName("ai_commentary")] string AiCommentary
);

public interface IAiDiagnosticClient
{
    Task<DiagnosticAnalyzeResponsePayload?> AnalyzeDiagnosticAsync(
        DiagnosticAnalyzeRequestPayload payload,
        CancellationToken ct = default);
}
