using System;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

/// <summary>
/// Request tạo mới lịch buổi học Live Q&A trực tuyến (Core Flow 2 - API 8)
/// </summary>
public class CreateLiveSessionRequestDto
{
    public Guid ClassId { get; set; }
    public Guid? TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string? MeetingUrl { get; set; }
}

/// <summary>
/// DTO thông tin chi tiết buổi học Live Q&A
/// </summary>
public class LiveSessionSummaryDto
{
    public Guid SessionId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid? TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? MeetingUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public bool IsRecorded { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
