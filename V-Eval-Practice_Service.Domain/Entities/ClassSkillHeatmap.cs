using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho từng ô ma trận nhiệt P(Lt) của học sinh cho từng kỹ năng trong lớp học (Core Flow 5)
/// </summary>
public class ClassSkillHeatmap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// Mã phân môn ĐGNL: DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI
    /// </summary>
    public string DomainCode { get; set; } = string.Empty;

    /// <summary>
    /// Xác suất nắm vững kiến thức P(Lt) từ 0.0000 đến 1.0000 (BKT / IRT)
    /// </summary>
    public double PltValue { get; set; } = 0.1000;

    /// <summary>
    /// Phân cấp độ làm chủ kiến thức: CRITICAL (<0.30), WEAK (0.30-0.50), DEVELOPING (0.50-0.70), MASTERED (>=0.70)
    /// </summary>
    public string MasteryLevel { get; set; } = "WEAK";

    /// <summary>
    /// Nguồn dữ liệu đánh giá gần nhất: DIAGNOSTIC, ADAPTIVE_QUIZ, MOCK_EXAM
    /// </summary>
    public string LastAssessmentType { get; set; } = "DIAGNOSTIC";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual Class? Class { get; set; }
}
