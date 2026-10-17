using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho bản ghi tổng hợp thống kê theo cột kỹ năng của lớp học phục vụ cảnh báo sư phạm (Core Flow 5)
/// </summary>
public class ClassHeatmapSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// Mã phân môn ĐGNL: DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI
    /// </summary>
    public string DomainCode { get; set; } = string.Empty;

    /// <summary>
    /// Điểm trung bình làm chủ kiến thức P(Lt) của cả lớp ở kỹ năng này
    /// </summary>
    public double AveragePlt { get; set; } = 0.0;

    /// <summary>
    /// Số lượng học sinh trong lớp có P(Lt) < 0.50
    /// </summary>
    public int StrugglingCount { get; set; } = 0;

    /// <summary>
    /// Tổng sĩ số học sinh của lớp học
    /// </summary>
    public int TotalStudents { get; set; } = 0;

    /// <summary>
    /// Tỷ lệ học sinh yếu kém ở kỹ năng này: StrugglingCount / TotalStudents
    /// </summary>
    public double StrugglingRate { get; set; } = 0.0;

    /// <summary>
    /// Độ lệch chuẩn mẫu sigma về năng lực của học sinh trong lớp đối với kỹ năng này
    /// </summary>
    public double StdDevPlt { get; set; } = 0.0;

    /// <summary>
    /// Trạng thái cảnh báo sư phạm: CRITICAL_RED, WARNING_AMBER, SAFE_GREEN
    /// </summary>
    public string AlertStatus { get; set; } = "SAFE_GREEN";

    /// <summary>
    /// Cờ cảnh báo phân hóa 2 cực cực đoan (Bimodal Distribution): sigma >= 0.28 và struggling_rate >= 0.30
    /// </summary>
    public bool IsBimodal { get; set; } = false;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual Class? Class { get; set; }
}
