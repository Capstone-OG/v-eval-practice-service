using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho bản ghi điểm danh và hoàn thành bài Quiz bù khi vắng mặt buổi Live Q&A
/// </summary>
public class LiveSessionAttendance
{
    public Guid AttendanceId { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }

    // Trạng thái điểm danh: ATTENDED, ABSENT
    public string AttendanceStatus { get; set; } = "ATTENDED";
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }

    // Trường phục vụ Unhappy Case 3: Quiz bù khi vắng mặt
    public Guid? MakeupQuizId { get; set; }
    public bool IsMakeupQuizPassed { get; set; } = false;

    // Navigation Properties
    public virtual LiveSession Session { get; set; } = null!;
}
