using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Thực thể Nhóm học tập vi mô trong lớp (Micro Study Group: 3 - 5 học sinh)
/// Phục vụ mô hình kèm cặp trực tiếp tại bàn, trao đổi cùng năng lực và phân phối đề thích ứng.
/// </summary>
public class ClassGroup
{
    public Guid GroupId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? FocusArea { get; set; }
    public string? CommonWeakSkillIds { get; set; }
    public string? RecommendedWorksheetTitle { get; set; }
    public string? AssignedWorksheetId { get; set; }
    public string? AssignedWorksheetTitle { get; set; }
    public DateTime? WorksheetAssignedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Class? Class { get; set; }
    public virtual ICollection<ClassGroupMember> Members { get; set; } = new List<ClassGroupMember>();
}
