using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho tiến trình học tập thích ứng của học sinh trong 1 chặng học (Chu trình P-L-A-R)
/// </summary>
public class StageProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid RoadmapNodeId { get; set; }

    /// <summary>
    /// Bước hiện tại trong chu trình P-L-A-R: PREVIEW, LEARN, APPLY, REFLECT
    /// </summary>
    public string CurrentStep { get; set; } = "PREVIEW";

    /// <summary>
    /// Phần trăm thời lượng video bài giảng đã xem ở bước LEARN (cần >= 80% để mở khóa APPLY)
    /// </summary>
    public decimal VideoWatchPercentage { get; set; } = 0.00m;

    /// <summary>
    /// Xác suất thành thạo kiến thức P(Lt) hiện tại từ thuật toán BKT (mặc định khởi tạo 0.1000)
    /// </summary>
    public double BktMasteryPlt { get; set; } = 0.1000;

    /// <summary>
    /// Đếm số câu đúng liên tiếp có độ khó vận dụng b >= 0.50 (BR-01 cần >= 2)
    /// </summary>
    public int ConsecutiveAdvancedCorrect { get; set; } = 0;

    /// <summary>
    /// Đếm số câu sai liên tiếp (BR-03: đạt 3 sẽ rẽ nhánh Remedial)
    /// </summary>
    public int ConsecutiveIncorrect { get; set; } = 0;

    /// <summary>
    /// Trạng thái chặng học: IN_PROGRESS, REMEDIAL_REQUIRED, COMPLETED
    /// </summary>
    public string Status { get; set; } = "IN_PROGRESS";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual RoadmapNode? RoadmapNode { get; set; }
    public virtual ICollection<AdaptiveQuizAttempt> AdaptiveAttempts { get; set; } = new List<AdaptiveQuizAttempt>();
}
