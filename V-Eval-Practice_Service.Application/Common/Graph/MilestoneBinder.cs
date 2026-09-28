using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Graph;

/// <summary>
/// Thông tin tài nguyên liên kết cho một kỹ năng (Video lý thuyết, Quiz, Live Q&A).
/// </summary>
public record SkillResourceBinding(
    Guid SkillId,
    Guid? MaterialId,     // Bài giảng lý thuyết từ content.Materials
    Guid? QuizExamId,     // Bài Quiz củng cố 5-10 câu
    Guid? LiveSessionId   // Buổi Live Q&A tương ứng của lớp cơ sở
);

/// <summary>
/// Bộ tích hợp 3 thành phần (Milestone Binder):
///   1. Video lý thuyết (Content Service Materials)
///   2. Quiz củng cố (Question Bank)
///   3. Lịch Live Q&A (Practice Service LiveSessions)
/// 
/// Chuyển danh sách skill_id đã sắp xếp Topo thành danh sách RoadmapNode thực thể,
/// gắn kết 3 thành phần và thiết lập trạng thái State Machine ban đầu.
/// </summary>
public class MilestoneBinder
{
    /// <summary>
    /// Khởi tạo các chặng học (Milestones / RoadmapNodes) từ danh sách kỹ năng đã sắp xếp Topo.
    /// </summary>
    /// <param name="roadmapId">ID của lộ trình học tập</param>
    /// <param name="sortedSkillIds">Danh sách skill_id đã sắp xếp theo thứ tự Topo</param>
    /// <param name="resourceBindings">Ánh xạ skill_id -> tài nguyên (video, quiz, live)</param>
    /// <param name="prunedSkillIds">Tập skill_id đã bị cắt tỉa (SKIPPED_PRUNED)</param>
    /// <returns>Danh sách RoadmapNode đã gắn kết 3 thành phần với trạng thái State Machine</returns>
    public List<RoadmapNode> BindMilestones(
        Guid roadmapId,
        List<Guid> sortedSkillIds,
        Dictionary<Guid, SkillResourceBinding> resourceBindings,
        HashSet<Guid> prunedSkillIds)
    {
        var nodes = new List<RoadmapNode>();
        int stepOrder = 0;

        foreach (var skillId in sortedSkillIds)
        {
            stepOrder++;
            bool isPruned = prunedSkillIds.Contains(skillId);

            // Lấy tài nguyên gắn kết (nếu có)
            resourceBindings.TryGetValue(skillId, out var binding);

            var node = new RoadmapNode
            {
                NodeId = Guid.NewGuid(),
                RoadmapId = roadmapId,
                SkillId = skillId,
                StepOrder = stepOrder,
                MaterialId = binding?.MaterialId,
                QuizExamId = binding?.QuizExamId,
                LiveSessionId = binding?.LiveSessionId,
                IsPruned = isPruned,
                // State Machine: Chặng bị cắt tỉa => SKIPPED_PRUNED, Chặng đầu tiên chưa bị prune => IN_PROGRESS, còn lại => LOCKED
                Status = isPruned ? "SKIPPED_PRUNED" : "LOCKED"
            };

            nodes.Add(node);
        }

        // Quy tắc 1 (Mở khóa khởi đầu): Chặng đầu tiên CHƯA bị prune => IN_PROGRESS
        var firstActiveNode = nodes.FirstOrDefault(n => !n.IsPruned);
        if (firstActiveNode != null)
        {
            firstActiveNode.Status = "IN_PROGRESS";
            firstActiveNode.UnlockedAt = DateTime.UtcNow;
        }

        return nodes;
    }
}
