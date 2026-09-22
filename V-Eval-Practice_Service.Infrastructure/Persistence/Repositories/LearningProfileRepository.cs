using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class LearningProfileRepository : ILearningProfileRepository
{
    private readonly PracticeDbContext _context;
    private readonly ILogger<LearningProfileRepository> _logger;

    public LearningProfileRepository(PracticeDbContext context, ILogger<LearningProfileRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertSkillPriorsAsync(
        Guid studentId,
        IEnumerable<(Guid SkillId, double MasteryScore)> priors,
        CancellationToken ct = default)
    {
        var priorList = priors.ToList();
        if (priorList.Count == 0) return;

        var skillIds = priorList.Select(p => p.SkillId).ToList();

        // Query existing profiles for this student and these skills
        var existingProfiles = await _context.LearningProfiles
            .Where(p => p.StudentId == studentId && skillIds.Contains(p.SkillId))
            .ToDictionaryAsync(p => p.SkillId, p => p, ct);

        foreach (var (skillId, score) in priorList)
        {
            if (existingProfiles.TryGetValue(skillId, out var profile))
            {
                profile.MasteryScore = score;
                profile.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                var newProfile = new LearningProfile
                {
                    ProfileId = Guid.NewGuid(),
                    StudentId = studentId,
                    SkillId = skillId,
                    MasteryScore = score,
                    LastUpdated = DateTime.UtcNow
                };
                await _context.LearningProfiles.AddAsync(newProfile, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Upserted {Count} BKT skill priors for student {StudentId}",
            priorList.Count, studentId);
    }

    public async Task<IReadOnlyList<LearningProfile>> GetByStudentIdAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        return await _context.LearningProfiles
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .ToListAsync(ct);
    }
}
