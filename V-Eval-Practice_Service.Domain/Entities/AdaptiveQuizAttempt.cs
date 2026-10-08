using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho nhật ký trả lời thích ứng từng câu hỏi của học sinh trong bước APPLY
/// </summary>
public class AdaptiveQuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageProgressId { get; set; }
    public Guid StudentId { get; set; }
    public Guid QuestionId { get; set; }
    public string PatternId { get; set; } = string.Empty;

    public string SelectedOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }

    /// <summary>
    /// Tham số khảo thí IRT 2PL của câu hỏi
    /// </summary>
    public double ItemDifficultyB { get; set; }
    public double ItemDiscriminationA { get; set; }

    /// <summary>
    /// Cờ đánh dấu trả lời đúng câu khó (b >= 0.5) trong thời gian quá nhanh (< 5s)
    /// </summary>
    public bool IsLuckyGuess { get; set; } = false;

    /// <summary>
    /// Xác suất nắm vững kiến thức trước và sau khi làm câu này từ BKT
    /// </summary>
    public double PriorPlt { get; set; }
    public double PosteriorPlt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual StageProgress StageProgress { get; set; } = null!;
}
