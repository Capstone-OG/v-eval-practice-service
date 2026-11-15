using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Thực thể Học kỳ / Đợt đào tạo chính thức (Conceptual Entity: Term)
/// Schema: v_eval_enrollment."AcademicTerms"
/// </summary>
public class AcademicTerm
{
    public Guid TermId { get; set; } = Guid.NewGuid();
    public Guid CampusId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = "2026-2027";
    public int TermOrder { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "ONGOING"; // UPCOMING, ONGOING, COMPLETED
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<CourseRegistration> Registrations { get; set; } = new List<CourseRegistration>();
}

