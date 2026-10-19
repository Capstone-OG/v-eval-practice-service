using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Analytics.DTOs;

public record RadarCompetencyDto(
    string DomainCode,
    string DomainName,
    double StudentScore,
    double TargetScore
);

public record RoadmapProgressDto(
    int TotalMilestones,
    int CompletedMilestones,
    double ProgressPercentage,
    string CurrentMilestoneTitle
);

public record StreakAnalyticsDto(
    int CurrentStreak,
    int LongestStreak,
    bool HasStreakFreeze,
    bool IsTodayCompleted,
    List<bool> WeeklyActivityDays
);

public record RecentMockExamDto(
    Guid SubmissionId,
    string ExamTitle,
    int Score,
    int TotalCorrect,
    int TotalQuestions,
    DateTime CompletedAt
);

public record StudentDashboardResponseDto(
    Guid StudentId,
    string FullName,
    int CurrentEstimatedScore,
    int TargetScore,
    string ConfidenceInterval,
    List<RadarCompetencyDto> RadarCompetency,
    RoadmapProgressDto RoadmapProgress,
    StreakAnalyticsDto StreakAnalytics,
    List<RecentMockExamDto> RecentMockExams,
    bool NeedsOnboarding
);
