using System;

namespace V_Eval_Practice_Service.Domain.Entities;

public class LearningProfile
{
    public Guid ProfileId { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public double MasteryScore { get; set; } = 0.0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
