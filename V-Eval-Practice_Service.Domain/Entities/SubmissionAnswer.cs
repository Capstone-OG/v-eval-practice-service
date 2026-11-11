using System;

namespace V_Eval_Practice_Service.Domain.Entities;

public class SubmissionAnswer
{
    public Guid AnswerId { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public string? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }
    public int TimeSpentSeconds { get; set; }

    // Navigation property
    public virtual ExamSubmission Submission { get; set; } = null!;
}
