using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class LiveSessionScheduleItemDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? MeetingUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public bool IsRecorded { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MyAttendanceStatus { get; set; }
    public bool? IsMakeupQuizPassed { get; set; }
}

public class MyLiveScheduleDto
{
    public Guid StudentId { get; set; }
    public Guid? EnrolledClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public List<LiveSessionScheduleItemDto> Sessions { get; set; } = new();
}
