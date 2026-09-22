using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;

public class QuestionAnswerInputDto
{
    public Guid QuestionId { get; set; }
    public string? SelectedOption { get; set; }
    public int TimeSpentSeconds { get; set; }
}

public class SkillDiagnosticDto
{
    public string SkillId { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public double AccuracyPercentage { get; set; }
    public bool IsWeak { get; set; }
}

public class DifficultyBreakdownDto
{
    public int DifficultyLevel { get; set; }
    public string LevelName { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public double AccuracyPercentage { get; set; }
}

public class QuestionResultDto
{
    public Guid QuestionId { get; set; }
    public int QuestionOrder { get; set; }
    public string? SelectedOption { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }
    public string SkillId { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
}

public class SubmitDiagnosticResponseDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamType { get; set; } = "DIAGNOSTIC";
    public int TotalScore { get; set; } // Raw score: 0 - 30
    public int TotalCorrect { get; set; }
    public int TotalQuestions { get; set; }
    public double AccuracyPercentage { get; set; }
    public int TotalTimeSpentSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    // Diagnostic breakdown by Skill & Difficulty
    public List<SkillDiagnosticDto> SkillBreakdowns { get; set; } = new();
    public List<string> WeakSkillIds { get; set; } = new();
    public List<DifficultyBreakdownDto> DifficultyBreakdowns { get; set; } = new();

    // Detailed question answers
    public List<QuestionResultDto> Questions { get; set; } = new();

    // IRT 2PL & Placement Results (Core Flow 1 - Steps 4 & 5)
    public double Theta0 { get; set; }
    public string PlacementClass { get; set; } = "ACCELERATION";
    public string ClassName { get; set; } = string.Empty;
    public Guid? ClassId { get; set; }
    public Guid? EnrollmentId { get; set; }
    public string AiCommentary { get; set; } = string.Empty;

    // Radar Chart & BKT Initial Mastery Priors
    public List<DiagnosticRadarAxisDto> RadarChart { get; set; } = new();
    public List<DiagnosticSkillPriorDto> SkillPriors { get; set; } = new();
}

public class DiagnosticRadarAxisDto
{
    public string DomainId { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public double StudentPct { get; set; }
    public double BenchmarkPct { get; set; }
}

public class DiagnosticSkillPriorDto
{
    public string SkillId { get; set; } = string.Empty;
    public string DomainId { get; set; } = string.Empty;
    public double ThetaSkill { get; set; }
    public double PL0 { get; set; }
    public string Source { get; set; } = "measured";
}

public class DiagnosticSubmissionSummaryDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamType { get; set; } = "DIAGNOSTIC";
    public int TotalScore { get; set; } // Raw score: 0 - 30
    public int TotalCorrect { get; set; }
    public int TotalQuestions { get; set; }
    public double AccuracyPercentage { get; set; }
    public int TotalTimeSpentSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
