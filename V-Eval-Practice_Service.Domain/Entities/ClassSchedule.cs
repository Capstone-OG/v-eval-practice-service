using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Khung lịch học định kỳ hàng tuần của Lớp học (Thứ trong tuần, Ca học, Giờ bắt đầu/kết thúc, Phòng học)
/// Dùng để kiểm tra trùng lịch khi xếp lớp và làm mẫu tự động rải các buổi học (ClassSessions) cho cả học kỳ.
/// Schema: v_eval_practice."ClassSchedules"
/// </summary>
public class ClassSchedule
{
    public Guid ScheduleId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    /// <summary>
    /// Thứ trong tuần theo chuẩn Việt Nam: 2 = Thứ Hai, 3 = Thứ Ba, ..., 7 = Thứ Bảy, 8 = Chủ Nhật
    /// </summary>
    public int DayOfWeek { get; set; } = 2;
    /// <summary>
    /// Ca học trong ngày: 1 = Ca 1 (07:30-09:30), 2 = Ca 2 (09:45-11:45), 3 = Ca 3 (13:30-15:30), 4 = Ca 4 (15:45-17:45), 5 = Ca 5 (18:00-20:00), 6 = Ca 6 (20:00-21:30)
    /// </summary>
    public int SlotNumber { get; set; } = 1;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Room { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Class? Class { get; set; }
    public virtual ICollection<ClassSession> GeneratedSessions { get; set; } = new List<ClassSession>();
}

