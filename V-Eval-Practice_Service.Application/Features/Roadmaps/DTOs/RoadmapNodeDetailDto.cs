using System;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO chi tiết buổi học Live Q&A liên kết với chặng học
/// </summary>
public record LiveSessionDetailDto(
    Guid SessionId,
    string Title,
    string? Description,
    DateTime ScheduledAt,
    int DurationMinutes,
    string? MeetingUrl,
    string? RecordingUrl,
    bool IsRecorded,
    string Status,
    string? AttendanceStatus,
    bool? IsMakeupQuizPassed
);

/// <summary>
/// DTO chi tiết toàn diện của 1 chặng học (Milestone / RoadmapNode) - Core Flow 2 API 3
/// </summary>
public record RoadmapNodeDetailDto(
    Guid NodeId,
    Guid RoadmapId,
    Guid StudentId,
    Guid SkillId,
    string SkillName,
    Guid DomainId,
    string DomainName,
    int StepOrder,
    string Status,
    bool IsPruned,
    DateTime? UnlockedAt,
    DateTime? CompletedAt,
    // Thành phần 1: Bài giảng lý thuyết video
    Guid? MaterialId,
    int VideoWatchedSeconds,
    int VideoTotalSeconds,
    bool IsVideoCompleted,
    bool IsQuizEligible,
    // Thành phần 2: Bài Quiz củng cố 5-10 câu
    Guid? QuizExamId,
    // Thành phần 3: Buổi học Live Q&A trực tuyến cơ sở
    LiveSessionDetailDto? LiveSession
);
