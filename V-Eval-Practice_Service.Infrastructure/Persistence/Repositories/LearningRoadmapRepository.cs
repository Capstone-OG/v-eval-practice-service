using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class LearningRoadmapRepository : ILearningRoadmapRepository
{
    private readonly PracticeDbContext _context;

    public LearningRoadmapRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<LearningRoadmap?> GetByIdAsync(Guid roadmapId, CancellationToken ct = default)
    {
        return await _context.LearningRoadmaps
            .Include(r => r.DiagnosticSubmission)
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .FirstOrDefaultAsync(r => r.RoadmapId == roadmapId, ct);
    }

    public async Task<LearningRoadmap?> GetActiveByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.LearningRoadmaps
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Status == "ACTIVE", ct);
    }

    public async Task<LearningRoadmap?> GetLatestByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        var active = await GetActiveByStudentIdAsync(studentId, ct);
        if (active != null) return active;

        return await _context.LearningRoadmaps
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .Where(r => r.StudentId == studentId && r.Status != "ARCHIVED")
            .OrderByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LearningRoadmap>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.LearningRoadmaps
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LearningRoadmap>> GetByStatusesAsync(IEnumerable<string> statuses, CancellationToken ct = default)
    {
        var statusSet = statuses.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToUpperInvariant()).ToList();
        var query = _context.LearningRoadmaps
            .Include(r => r.DiagnosticSubmission)
            .Include(r => r.Nodes)
            .AsQueryable();

        if (statusSet.Count > 0)
        {
            query = query.Where(r => statusSet.Contains(r.Status));
        }
        else
        {
            query = query.Where(r => r.Status != "ARCHIVED");
        }

        return await query
            .OrderByDescending(r => r.UpdatedAt)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LearningRoadmap roadmap, CancellationToken ct = default)
    {
        await _context.LearningRoadmaps.AddAsync(roadmap, ct);
    }

    public async Task AddNodeAsync(RoadmapNode node, CancellationToken ct = default)
    {
        await _context.RoadmapNodes.AddAsync(node, ct);
    }

    public async Task ArchiveExistingActiveRoadmapsAsync(Guid studentId, CancellationToken ct = default)
    {
        var existingActive = await _context.LearningRoadmaps
            .Where(r => r.StudentId == studentId && r.Status == "ACTIVE")
            .ToListAsync(ct);

        foreach (var r in existingActive)
        {
            r.Status = "ARCHIVED";
            r.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task ArchiveExistingDraftRoadmapsAsync(Guid studentId, CancellationToken ct = default)
    {
        var existingDrafts = await _context.LearningRoadmaps
            .Where(r => r.StudentId == studentId && (r.Status == "DRAFT_PENDING_TEACHER" || r.Status == "PENDING_DIRECTOR_APPROVAL"))
            .ToListAsync(ct);

        foreach (var r in existingDrafts)
        {
            r.Status = "ARCHIVED";
            r.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<RoadmapNode?> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        return await _context.RoadmapNodes
            .Include(n => n.Roadmap)
            .FirstOrDefaultAsync(n => n.NodeId == nodeId, ct);
    }

    public async Task<RoadmapNode?> GetNextLockedNodeAsync(Guid roadmapId, int currentStepOrder, CancellationToken ct = default)
    {
        return await _context.RoadmapNodes
            .Where(n => n.RoadmapId == roadmapId && n.StepOrder > currentStepOrder && !n.IsPruned && (n.Status == "LOCKED" || n.Status == "UNLOCKED"))
            .OrderBy(n => n.StepOrder)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<RoadmapNode>> GetNodesBySkillAndStudentIdsAsync(Guid skillId, IEnumerable<Guid> studentIds, CancellationToken ct = default)
    {
        var studentIdList = studentIds.ToList();
        return await _context.RoadmapNodes
            .Include(n => n.Roadmap)
            .Where(n => n.SkillId == skillId && studentIdList.Contains(n.Roadmap.StudentId) && n.Roadmap.Status == "ACTIVE")
            .ToListAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
