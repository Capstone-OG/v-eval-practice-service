using System;

namespace V_Eval_Practice_Service.Domain.Entities;

public class Class
{
    public Guid ClassId { get; set; } = Guid.NewGuid();
    public Guid CampusId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? AssignedBy { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public DateTime? AssignedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ===== Hỗ trợ Lớp Chuyên Đề (Thematic Cohort) =====
    public int ClassType { get; set; } = 0;        // 0 = Hành chính, 1 = Chuyên đề
    public Guid? DomainId { get; set; }             // Domain nào (nếu ClassType = 1)
    public string? DomainCode { get; set; }         // DOM_LANG, DOM_MATH, DOM_NAT_SCI, DOM_SOC_SCI
    public int? ClusterIndex { get; set; }          // Cụm K-Means nào tạo ra lớp này
}
