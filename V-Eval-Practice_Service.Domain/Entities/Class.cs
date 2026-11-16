using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

public class Class
{
    public Guid ClassId { get; set; } = Guid.NewGuid();
    public Guid CampusId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? AssignedBy { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AcademicYear { get; set; } = "2026-2027";
    public string Status { get; set; } = "ACTIVE";
    public int MaxCapacity { get; set; } = 20;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ===== Hỗ trợ Lớp Chuyên Đề (Thematic Cohort) =====
    public int ClassType { get; set; } = 0;        // 0 = Hành chính, 1 = Chuyên đề
    public Guid? DomainId { get; set; }             // Domain nào (nếu ClassType = 1)
    public string? DomainCode { get; set; }         // DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI
    public int? ClusterIndex { get; set; }          // Cụm K-Means nào tạo ra lớp này

    // Navigation properties
    public virtual AcademicTerm? Term { get; set; }
    public virtual Course? Course { get; set; }
    public virtual ICollection<ClassEnrollment> Enrollments { get; set; } = new List<ClassEnrollment>();
    public virtual ICollection<ClassGroup> Groups { get; set; } = new List<ClassGroup>();
    public virtual ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public virtual ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}
