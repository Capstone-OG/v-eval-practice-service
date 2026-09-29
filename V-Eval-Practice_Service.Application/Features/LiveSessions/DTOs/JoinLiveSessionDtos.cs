using System;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class JoinLiveSessionRequestDto
{
    public Guid? StudentId { get; set; }
}

public class JoinLiveSessionResponseDto
{
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MeetingUrl { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string AttendanceStatus { get; set; } = "ATTENDED";
    public DateTime JoinedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
