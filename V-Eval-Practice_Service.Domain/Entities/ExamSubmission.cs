using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

public class ExamSubmission
{
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    public string ExamType { get; set; } = "DIAGNOSTIC";
    public int TotalScore { get; set; }
    public int TotalCorrect { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalTimeSpentSeconds { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "COMPLETED";

    // Diagnostic IRT & Placement Fields (Core Flow 1)
    public double? Theta0 { get; set; }
    public string? PlacementClass { get; set; }
    public string? AiCommentary { get; set; }
    public Guid? EnrolledClassId { get; set; }

    // Navigation property
    public virtual ICollection<SubmissionAnswer> Answers { get; set; } = new List<SubmissionAnswer>();
}
