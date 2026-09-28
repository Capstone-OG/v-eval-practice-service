namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// Metadata bổ sung cho mỗi kỹ năng phục vụ hàm ưu tiên sư phạm trong Topological Sort.
/// </summary>
public record SkillSortMetadata(
    Guid SkillId,
    double PL0,      // P(L0): Xác suất làm chủ ban đầu từ Flow 1
    double Weight,    // Trọng số đề thi (0.0 - 1.0)
    bool IsWeak       // Kỹ năng yếu (accuracy < 60% trong bài chẩn đoán)
);

/// <summary>
/// Thuật toán Sắp xếp Topo đa tiêu chí (Kahn's Algorithm + Priority Queue).
/// 
/// Đảm bảo: Kỹ năng nền tảng (in-degree = 0) luôn đứng trước kỹ năng nâng cao.
/// Trong các kỹ năng cùng bậc, ưu tiên theo hàm PriorityScore:
///   PriorityScore(u) = (1.0 - P(L0)_u) * 0.5 + Weight_u * 0.3 + IsWeak_u * 0.2
///   - Kỹ năng hổng nặng hơn => ưu tiên cao hơn.
///   - Kỹ năng trọng số đề thi cao hơn => ưu tiên cao hơn.
/// </summary>
public class TopologicalSorter
{
    /// <summary>
    /// Sắp xếp danh sách kỹ năng theo thứ tự topo với ưu tiên sư phạm.
    /// </summary>
    /// <param name="candidateSkills">Danh sách skill_id cần sắp xếp (đã lọc sau cắt tỉa)</param>
    /// <param name="prerequisitesMap">Ánh xạ skill_id -> danh sách prerequisite skill_ids</param>
    /// <param name="metadataMap">Ánh xạ skill_id -> metadata (P(L0), Weight, IsWeak)</param>
    /// <returns>Danh sách skill_id đã sắp xếp theo thứ tự học tối ưu</returns>
    public List<Guid> Sort(
        List<Guid> candidateSkills,
        Dictionary<Guid, List<Guid>> prerequisitesMap,
        Dictionary<Guid, SkillSortMetadata> metadataMap)
    {
        var skillSet = new HashSet<Guid>(candidateSkills);
        var inDegree = new Dictionary<Guid, int>();
        var dependentsGraph = new Dictionary<Guid, List<Guid>>(); // prereq -> dependent skills

        foreach (var skill in skillSet)
        {
            inDegree[skill] = 0;
            dependentsGraph[skill] = new List<Guid>();
        }

        // Xây dựng in-degree và đồ thị phụ thuộc (chỉ xét các kỹ năng trong tập ứng viên)
        foreach (var skill in skillSet)
        {
            if (prerequisitesMap.TryGetValue(skill, out var prereqs))
            {
                foreach (var prereq in prereqs.Where(p => skillSet.Contains(p)))
                {
                    inDegree[skill]++;
                    dependentsGraph[prereq].Add(skill);
                }
            }
        }

        // Sử dụng PriorityQueue với Comparer giảm dần (PriorityScore cao nhất trước)
        var priorityQueue = new PriorityQueue<Guid, double>(Comparer<double>.Create((a, b) => b.CompareTo(a)));

        foreach (var skill in skillSet)
        {
            if (inDegree[skill] == 0)
            {
                double score = CalculateScore(skill, metadataMap);
                priorityQueue.Enqueue(skill, score);
            }
        }

        var sortedResult = new List<Guid>();

        while (priorityQueue.Count > 0)
        {
            var currentSkill = priorityQueue.Dequeue();
            sortedResult.Add(currentSkill);

            foreach (var dependent in dependentsGraph[currentSkill])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    double score = CalculateScore(dependent, metadataMap);
                    priorityQueue.Enqueue(dependent, score);
                }
            }
        }

        return sortedResult;
    }

    /// <summary>
    /// Hàm tính điểm ưu tiên sư phạm (Pedagogical Priority Scoring).
    /// PriorityScore = (1.0 - P(L0)) * 0.5 + Weight * 0.3 + IsWeak * 0.2
    /// </summary>
    private static double CalculateScore(Guid skillId, Dictionary<Guid, SkillSortMetadata> metadataMap)
    {
        if (!metadataMap.TryGetValue(skillId, out var meta)) return 0.5;
        double weakBonus = meta.IsWeak ? 0.2 : 0.0;
        return (1.0 - meta.PL0) * 0.5 + meta.Weight * 0.3 + weakBonus;
    }
}
