using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Quản lý lộ trình học tập cá nhân hóa của học sinh (Core Flow 2)
/// </summary>
public class LearningRoadmap
{
    public Guid RoadmapId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid? DiagnosticSubmissionId { get; set; }
    public int TargetScore { get; set; } = 800;
    public int TotalMilestones { get; set; } = 0;
    public int CompletedMilestones { get; set; } = 0;
    public bool IsPruned { get; set; } = false;
    public string? PrunedReason { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, COMPLETED, ARCHIVED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual ExamSubmission? DiagnosticSubmission { get; set; }
    public virtual ICollection<RoadmapNode> Nodes { get; set; } = new List<RoadmapNode>();
}
