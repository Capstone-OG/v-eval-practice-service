using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Thành viên thuộc Nhóm học tập vi mô
/// </summary>
public class ClassGroupMember
{
    public Guid GroupMemberId { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ClassGroup? Group { get; set; }
}
