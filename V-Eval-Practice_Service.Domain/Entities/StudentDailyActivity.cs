using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho nhật ký hoạt động học tập chuyên cần và thời gian học hằng ngày của học sinh (Core Flow 5 - Streak)
/// </summary>
public class StudentDailyActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }

    /// <summary>
    /// Ngày diễn ra hoạt động (chỉ lấy phần Date, lưu dạng date trong PostgreSQL)
    /// </summary>
    public DateTime ActivityDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    /// <summary>
    /// Tổng thời gian học tập tích lũy trong ngày (phút)
    /// </summary>
    public int StudyTimeMinutes { get; set; } = 0;

    /// <summary>
    /// Số câu hỏi luyện tập thích ứng APPLY đã hoàn thành trong ngày
    /// </summary>
    public int PracticeQuestionsSolved { get; set; } = 0;

    /// <summary>
    /// Số phút xem video bài giảng lý thuyết LEARN trong ngày
    /// </summary>
    public int VideoMinutesWatched { get; set; } = 0;

    /// <summary>
    /// Số bài thi thử mô phỏng hoặc bài chẩn đoán đã hoàn thành trong ngày
    /// </summary>
    public int MockExamsTaken { get; set; } = 0;

    /// <summary>
    /// Đánh dấu ngày này đã đạt chuẩn Active Study Day để tính vào Streak hay chưa
    /// Tiêu chuẩn: PracticeQuestionsSolved >= 1 HOẶC VideoMinutesWatched >= 15 HOẶC MockExamsTaken >= 1
    /// </summary>
    public bool IsStreakCounted { get; set; } = false;

    /// <summary>
    /// Cờ đánh dấu sử dụng vật phẩm bảo vệ chuỗi (Streak Freeze) trong ngày
    /// </summary>
    public bool IsFrozen { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
