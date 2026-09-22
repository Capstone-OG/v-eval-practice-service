using System;

namespace V_Eval_Practice_Service.Domain.Entities;

public class ClassEnrollment
{
    public Guid EnrollmentId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? DiagnosticSubmissionId { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string Status { get; set; } = "ENROLLED";
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    // Optional navigation
    public virtual Class? Class { get; set; }
}
