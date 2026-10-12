using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho một bản ghi câu hỏi làm sai trong Sổ tay lỗi sai cá nhân của học sinh
/// Phục vụ cơ chế Phản tư nhận thức (Metacognition) và Lặp lại ngắt quãng SM-2 (Spaced Repetition)
/// </summary>
public class MistakeNotebook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid SkillId { get; set; }
    public string PatternId { get; set; } = "STANDARD";

    /// <summary>
    /// Nhãn nhận thức sư phạm: CARELESS (Tính ẩu), MISREAD_QUESTION (Đọc sót đề), MISSING_CONCEPT (Hổng lý thuyết)
    /// </summary>
    public string? CognitiveErrorTag { get; set; }

    /// <summary>
    /// Ghi chú bài học kinh nghiệm cá nhân học sinh rút ra sau khi phản tư
    /// </summary>
    public string? StudentNotes { get; set; }

    /// <summary>
    /// Ngày hẹn ôn tập tiếp theo theo thuật toán lặp lại ngắt quãng SM-2 (mặc định: Ngày hôm sau)
    /// </summary>
    public DateTime NextReviewDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);

    /// <summary>
    /// Tổng số lần đã làm bài ôn tập cho câu hỏi / dạng bài này
    /// </summary>
    public int ReviewCount { get; set; } = 0;

    /// <summary>
    /// Số lần làm đúng liên tiếp trong các phiên ôn tập (đạt >= 3 lần -> IsMastered = true)
    /// </summary>
    public int ConsecutiveCorrectReviews { get; set; } = 0;

    /// <summary>
    /// Khoảng cách số ngày ôn tập cho lần tiếp theo (Interval Days theo SM-2)
    /// </summary>
    public int IntervalDays { get; set; } = 1;

    /// <summary>
    /// Hệ số dễ (Ease Factor) của thuật toán SuperMemo-2 (mặc định khởi tạo 2.50)
    /// </summary>
    public double EaseFactor { get; set; } = 2.50;

    /// <summary>
    /// Trạng thái đã khắc phục triệt để lỗ hổng tri thức hay chưa
    /// </summary>
    public bool IsMastered { get; set; } = false;

    /// <summary>
    /// Thời điểm làm bài ôn tập gần nhất
    /// </summary>
    public DateTime? LastReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
