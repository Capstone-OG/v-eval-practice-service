using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Khóa học do cơ sở mở: gói, thời lượng, học phí, chính sách học lại, sĩ số tối đa.
/// Schema: v_eval_enrollment."Courses"
/// </summary>
public class Course
{
    public Guid CourseId { get; set; } = Guid.NewGuid();
    public Guid CampusId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanType { get; set; } = "STANDARD";
    public int DurationWeeks { get; set; } = 12;
    public decimal TuitionFee { get; set; }
    public string RetakePolicy { get; set; } = "PAID";
    public decimal RetakeFee { get; set; } = 0;
    public int MaxClassSize { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
}

