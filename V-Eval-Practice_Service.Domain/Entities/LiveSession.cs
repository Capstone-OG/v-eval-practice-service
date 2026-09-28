using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho buổi học trực tuyến Live Q&A giải đáp thắc mắc do giáo viên cơ sở chủ trì
/// </summary>
public class LiveSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid? TeacherId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string? MeetingUrl { get; set; }

    // Trường phục vụ Unhappy Case 3 (Học sinh vắng mặt xem video ghi hình)
    public string? RecordingUrl { get; set; }
    public bool IsRecorded { get; set; } = false;

    public string Status { get; set; } = "SCHEDULED"; // SCHEDULED, LIVE, COMPLETED, CANCELLED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual Class Class { get; set; } = null!;
    public virtual ICollection<LiveSessionAttendance> Attendances { get; set; } = new List<LiveSessionAttendance>();
    public virtual ICollection<RoadmapNode> RoadmapNodes { get; set; } = new List<RoadmapNode>();
}
