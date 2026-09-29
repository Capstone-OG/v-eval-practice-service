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
            .Include(r => r.Nodes)
            .FirstOrDefaultAsync(r => r.RoadmapId == roadmapId, ct);
    }

    public async Task<LearningRoadmap?> GetActiveByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.LearningRoadmaps
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.Status == "ACTIVE", ct);
    }

    public async Task<IReadOnlyList<LearningRoadmap>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.LearningRoadmaps
            .Include(r => r.Nodes.OrderBy(n => n.StepOrder))
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LearningRoadmap roadmap, CancellationToken ct = default)
    {
        await _context.LearningRoadmaps.AddAsync(roadmap, ct);
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

    public async Task<LiveSession?> GetUpcomingLiveSessionAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.LiveSessions
            .Where(ls => ls.ClassId == classId && ls.Status == "SCHEDULED")
            .OrderBy(ls => ls.ScheduledAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RoadmapNode?> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        return await _context.RoadmapNodes
            .Include(n => n.Roadmap)
            .Include(n => n.LiveSession)
            .FirstOrDefaultAsync(n => n.NodeId == nodeId, ct);
    }

    public async Task<LiveSessionAttendance?> GetAttendanceAsync(Guid liveSessionId, Guid studentId, CancellationToken ct = default)
    {
        return await _context.LiveSessionAttendances
            .FirstOrDefaultAsync(a => a.SessionId == liveSessionId && a.StudentId == studentId, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
