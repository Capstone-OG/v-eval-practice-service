using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Buổi học / Ca lên lớp cụ thể từng ngày của một Lớp học (Conceptual Entity: Session)
/// Hỗ trợ quản lý bài giảng trọng tâm (SkillId/TopicTitle), giảng viên đứng lớp/dạy thay (TeacherId),
/// dời lịch/học bù (RescheduledFromId) và điểm danh học sinh (Attendances).
/// Schema: v_eval_practice."ClassSessions"
/// </summary>
public class ClassSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid? ScheduleId { get; set; }
    public int SessionNo { get; set; } = 1;
    public Guid? SkillId { get; set; }
    public string? TopicTitle { get; set; }
    public Guid? TeacherId { get; set; }
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Room { get; set; }
    /// <summary>
    /// SCHEDULED, ONGOING, COMPLETED, CANCELLED, RESCHEDULED
    /// </summary>
    public string Status { get; set; } = "SCHEDULED";
    public Guid? RescheduledFromId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Class? Class { get; set; }
    public virtual ClassSchedule? Schedule { get; set; }
    public virtual ClassSession? RescheduledFromSession { get; set; }
    public virtual ICollection<ClassSession> MakeupSessions { get; set; } = new List<ClassSession>();
    public virtual ICollection<SessionAttendance> Attendances { get; set; } = new List<SessionAttendance>();
}

