using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đăng ký khóa học của học sinh; attempt_no và previous_registration_id biểu diễn các lần học lại
/// Schema: v_eval_enrollment."CourseRegistrations"
/// </summary>
public class CourseRegistration
{
    public Guid RegistrationId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? PreviousRegistrationId { get; set; }
    public int AttemptNo { get; set; } = 1;
    public string Status { get; set; } = "PENDING_PAYMENT"; // PENDING_PAYMENT, ACTIVE, COMPLETED, CANCELLED
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // Navigation Properties
    public virtual Course? Course { get; set; }
    public virtual CourseRegistration? PreviousRegistration { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

