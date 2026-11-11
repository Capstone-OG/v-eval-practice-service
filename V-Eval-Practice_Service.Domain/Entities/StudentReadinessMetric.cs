using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho chỉ số sẵn sàng thi và tiến độ chuyên cần định kỳ phục vụ Cổng Phụ huynh & Học sinh (Core Flow 5)
/// </summary>
public class StudentReadinessMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }

    /// <summary>
    /// Điểm năng lực chuẩn hóa từ ước lượng Theta IRT (thang điểm 0 - 100)
    /// </summary>
    public double NormalizedTheta { get; set; } = 50.0;

    /// <summary>
    /// Mức độ bao phủ thành thạo kiến thức toàn diện BKT (thang điểm 0 - 100)
    /// </summary>
    public double SkillCoveragePlt { get; set; } = 0.0;

    /// <summary>
    /// Điểm số kỷ luật và tính chuyên cần dựa trên Streak và số giờ tự học (thang điểm 0 - 100)
    /// </summary>
    public double ConsistencyScore { get; set; } = 0.0;

    /// <summary>
    /// Chỉ số sẵn sàng thi tổng hợp: R_exam = 0.50*NormalizedTheta + 0.35*SkillCoveragePlt + 0.15*ConsistencyScore
    /// </summary>
    public double ExamReadinessIndex { get; set; } = 0.0;

    /// <summary>
    /// Phân loại cấp bậc sẵn sàng: NOT_READY (<50%), PREPARING (50-65%), ACCELERATING (65-75%), READY (>75%)
    /// </summary>
    public string ReadinessTier { get; set; } = "PREPARING";

    /// <summary>
    /// Tổng số giờ tự học trong 7 ngày gần nhất (định mức tối thiểu 10h/tuần)
    /// </summary>
    public double StudyHoursThisWeek { get; set; } = 0.0;

    /// <summary>
    /// Chuỗi ngày học chuyên cần hiện tại (Active Study Days liên tục)
    /// </summary>
    public int CurrentStreakDays { get; set; } = 0;

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
