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
}
