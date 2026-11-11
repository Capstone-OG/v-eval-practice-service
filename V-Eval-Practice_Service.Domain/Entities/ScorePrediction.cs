using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho bản ghi dự báo khoảng điểm chuẩn hóa và toa thuốc bù điểm Knapsack ROI (Core Flow 6)
/// </summary>
public class ScorePrediction
{
    public Guid PredictionId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }

    /// <summary>
    /// Điểm dự đoán trung tâm (Thang 1200 điểm ĐGNL ĐHQG-HCM)
    /// </summary>
    public int? PredictedScore { get; set; }

    /// <summary>
    /// Cận dưới của dải điểm tin cậy (PredictedScore - SEM * 1.96)
    /// </summary>
    public int? PredictedScoreMin { get; set; }

    /// <summary>
    /// Cận trên của dải điểm tin cậy (PredictedScore + SEM * 1.96)
    /// </summary>
    public int? PredictedScoreMax { get; set; }

    /// <summary>
    /// Độ tin cậy thống kê (Mặc định: 0.95 tương ứng khoảng tin cậy 95%)
    /// </summary>
    public double? ConfidenceRate { get; set; } = 0.95;

    /// <summary>
    /// Điểm số mục tiêu mà học sinh đã đăng ký
    /// </summary>
    public int? TargetScore { get; set; }

    /// <summary>
    /// Độ thâm hụt điểm số: Math.Max(0, TargetScore - PredictedScore)
    /// </summary>
    public int? DeficitScore { get; set; }

    /// <summary>
    /// Sai số đo lường chuẩn (Standard Error of Measurement - SEM)
    /// </summary>
    public double? SemScore { get; set; }

    /// <summary>
    /// Danh sách các dạng bài bù điểm tối ưu theo thuật toán Greedy Knapsack ROI (JSONB string)
    /// </summary>
    public string? PrescriptionItems { get; set; }

    /// <summary>
    /// Nguồn gốc kích hoạt tính toán: 'MOCK_EXAM', 'STAGE_COMPLETION', 'MANUAL_RECALCULATE'
    /// </summary>
    public string CalculationSource { get; set; } = "MOCK_EXAM";

    /// <summary>
    /// Thời điểm khởi tạo bản ghi dự báo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
