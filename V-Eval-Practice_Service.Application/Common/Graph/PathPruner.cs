namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// Dữ liệu đầu vào cho thuật toán cắt tỉa quỹ thời gian.
/// </summary>
public record PruningContext(
    DateTime ExamDate,
    DateTime Now,
    double StudyHoursPerDay,
    int TargetScore,
    Dictionary<Guid, double> SkillWeights,       // skill_id -> trọng số đề thi (0.0 - 1.0)
    Dictionary<Guid, double> SkillMasteryPL0,     // skill_id -> P(L0) xác suất làm chủ ban đầu
    double EstimatedHoursPerSkill = 4.0            // Quy chuẩn V-Eval: 1.5h lý thuyết + 1.5h bài tập + 1h Live Q&A
);

/// <summary>
/// Kết quả cắt tỉa cho một kỹ năng.
/// </summary>
public record PruningResult(
    Guid SkillId,
    bool IsPruned,
    string? PrunedReason
);

/// <summary>
/// Kết quả tổng hợp của thuật toán cắt tỉa.
/// </summary>
public record PruningOutcome(
    bool IsOverloaded,
    double AvailableHours,
    double RequiredHours,
    string? PrunedReason,
    List<PruningResult> SkillResults
);

/// <summary>
/// Thuật toán Phân Tích Quỹ Thời Gian & Cắt Tỉa Lộ Trình (Path Pruning Engine).
/// 
/// Chiến lược cắt tỉa 3 tầng:
///   Tầng 1: Loại bỏ kỹ năng có trọng số dưới 5% trong cấu trúc đề thi.
///   Tầng 2: Loại bỏ kỹ năng đã đạt chuẩn P(L0) >= 0.85.
///   Tầng 3: Dồn 80% thời gian vào Toán logic và Đọc hiểu (trọng tâm điểm rơi).
/// </summary>
public class PathPruner
{
    private const double WeightPruneThreshold = 0.05;  // Tầng 1: Trọng số < 5%
    private const double MasteryPruneThreshold = 0.85; // Tầng 2: P(L0) >= 85%
    private const int UrgentDaysThreshold = 30;        // Ngày gấp rút kích hoạt cắt tỉa
    private const int UrgentScoreThreshold = 800;      // Điểm mục tiêu kích hoạt cắt tỉa gấp rút

    /// <summary>
    /// Thực hiện phân tích quỹ thời gian và quyết định cắt tỉa kỹ năng.
    /// </summary>
    public PruningOutcome Analyze(PruningContext context, List<Guid> candidateSkillIds)
    {
        // 1. Tính quỹ thời gian khả dụng (giờ)
        double daysRemaining = Math.Max(1, (context.ExamDate - context.Now).TotalDays);
        double availableHours = daysRemaining * context.StudyHoursPerDay;

        // 2. Tính tổng thời lượng cần thiết (giờ)
        double requiredHours = candidateSkillIds.Count * context.EstimatedHoursPerSkill;

        // 3. Kiểm tra điều kiện kích hoạt cắt tỉa
        bool isOverloaded = availableHours < requiredHours
            || (daysRemaining < UrgentDaysThreshold && context.TargetScore >= UrgentScoreThreshold);

        var results = new List<PruningResult>();

        if (!isOverloaded)
        {
            // Không cần cắt tỉa, giữ trọn vẹn toàn bộ kỹ năng
            foreach (var skillId in candidateSkillIds)
            {
                results.Add(new PruningResult(skillId, false, null));
            }

            return new PruningOutcome(false, availableHours, requiredHours, null, results);
        }

        // 4. Áp dụng chiến lược cắt tỉa 3 tầng
        foreach (var skillId in candidateSkillIds)
        {
            double weight = context.SkillWeights.GetValueOrDefault(skillId, 0.05);
            double mastery = context.SkillMasteryPL0.GetValueOrDefault(skillId, 0.5);

            // Tầng 1: Loại bỏ kỹ năng trọng số thấp < 5%
            if (weight < WeightPruneThreshold)
            {
                results.Add(new PruningResult(skillId, true,
                    $"Cắt tỉa Tầng 1: Trọng số đề thi ({weight:P0}) dưới ngưỡng 5%. Dồn thời gian cho chuyên đề trọng tâm."));
                continue;
            }

            // Tầng 2: Loại bỏ kỹ năng đã đạt chuẩn P(L0) >= 85%
            if (mastery >= MasteryPruneThreshold)
            {
                results.Add(new PruningResult(skillId, true,
                    $"Cắt tỉa Tầng 2: Năng lực ban đầu P(L0) = {mastery:F2} đã đạt chuẩn (>= 85%). Không cần ôn lại."));
                continue;
            }

            // Tầng 3: Giữ lại kỹ năng trọng tâm (trọng số >= 5% và chưa đạt chuẩn)
            results.Add(new PruningResult(skillId, false, null));
        }

        // Tạo lý do cắt tỉa tổng hợp
        int prunedCount = results.Count(r => r.IsPruned);
        int keptCount = results.Count(r => !r.IsPruned);
        string prunedReason = $"Quỹ thời gian còn lại {daysRemaining:F0} ngày ({availableHours:F0}h khả dụng vs {requiredHours:F0}h cần thiết). "
            + $"Hệ thống đã tối ưu cắt tỉa {prunedCount} chuyên đề trọng số thấp/đã đạt chuẩn, giữ lại {keptCount} chuyên đề trọng tâm.";

        return new PruningOutcome(true, availableHours, requiredHours, prunedReason, results);
    }
}
