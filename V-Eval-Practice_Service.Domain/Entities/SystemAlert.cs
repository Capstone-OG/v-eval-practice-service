using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho một cảnh báo sớm 3 cấp (BR-04) và nhật ký can thiệp sư phạm của Mentor (Core Flow 6)
/// </summary>
public class SystemAlert
{
    public Guid AlertId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }

    /// <summary>
    /// Mức độ cảnh báo: 'GREEN' (An toàn), 'YELLOW' (Cảnh báo nhắc nhở), 'RED' (Nguy cơ cao), 'UNREALISTIC_GOAL' (Mục tiêu không tưởng)
    /// </summary>
    public string AlertType { get; set; } = "GREEN";

    /// <summary>
    /// Nội dung chi tiết giải thích nguyên nhân cảnh báo
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Cờ đánh dấu học sinh đã đọc thông báo hay chưa
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Mã định danh của Giáo viên / Mentor tiếp nhận và xử lý cảnh báo
    /// </summary>
    public Guid? HandledBy { get; set; }

    /// <summary>
    /// Ghi chú giải trình hoặc kết quả can thiệp sư phạm tại lớp của Mentor
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Thời điểm Mentor bấm xác nhận xử lý thành công
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Thời điểm kích hoạt cảnh báo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
