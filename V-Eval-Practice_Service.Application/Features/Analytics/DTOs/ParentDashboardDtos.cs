using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

public record ExamReadinessDto(
    double ReadinessIndex,
    string ReadinessTier,
    double NormalizedThetaScore,
    double SkillCoverageScore,
    double ConsistencyScore
);

public record WeeklyDisciplinesDto(
    double StudyHoursThisWeek,
    double RecommendedHours,
    int CurrentStreak,
    string CompletionStatus
);

public record AiParentAlertDto(
    string Type,
    string Title,
    string Description
);

public record ChildAnalyticsDto(
    Guid StudentId,
    string StudentName,
    string Grade,
    string School,
    string TargetMajor,
    string AssignedTeacherName,
    string TeacherContactPhone,
    ExamReadinessDto ExamReadiness,
    WeeklyDisciplinesDto WeeklyDisciplines,
    List<AiParentAlertDto> AiParentAlerts
);

public record ParentDashboardResponseDto(
    Guid ParentId,
    List<ChildAnalyticsDto> Children
);
