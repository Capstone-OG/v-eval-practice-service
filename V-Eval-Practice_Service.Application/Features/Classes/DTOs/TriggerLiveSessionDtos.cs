using System;

namespace V_Eval_Practice_Service.Application.Features.Classes.DTOs;

public record TriggerLiveSessionRequestDto(
    Guid SkillId,
    string SessionTitle,
    DateTime ScheduledAt,
    int DurationMinutes,
    string? MeetingUrl
);

public record TriggerLiveSessionResponseDto(
    Guid SessionId,
    Guid ClassId,
    string Title,
    DateTime ScheduledAt,
    int TargetedStudentsCount,
    string Status,
    string Message
);
