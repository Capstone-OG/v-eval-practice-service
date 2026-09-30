using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

public class TeacherLiveSessionItemDto
{
    public Guid SessionId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? MeetingUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public bool IsRecorded { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalEnrolledStudents { get; set; }
    public int TotalAttended { get; set; }
    public int TotalAbsent { get; set; }
}

public class TeacherScheduleDto
{
    public Guid TeacherId { get; set; }
    public int TotalSessions { get; set; }
    public List<TeacherLiveSessionItemDto> Sessions { get; set; } = new();
}
