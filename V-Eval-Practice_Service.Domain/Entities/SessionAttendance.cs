using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Điểm danh chuyên cần của từng Học sinh tại từng Buổi học cụ thể (Phân rã từ User }o--o{ Session)
/// Schema: v_eval_practice."SessionAttendances"
/// </summary>
public class SessionAttendance
{
    public Guid AttendanceId { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? EnrollmentId { get; set; }
    /// <summary>
    /// PRESENT (Có mặt), ABSENT (Vắng không phép), LATE (Đi muộn), EXCUSED (Vắng có phép)
    /// </summary>
    public string Status { get; set; } = "PRESENT";
    public DateTime? CheckInAt { get; set; }
    public Guid? RecordedBy { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ClassSession? Session { get; set; }
    public virtual ClassEnrollment? Enrollment { get; set; }
}

