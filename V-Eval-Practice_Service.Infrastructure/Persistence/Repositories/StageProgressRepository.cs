using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class StageProgressRepository : IStageProgressRepository
{
    private readonly PracticeDbContext _context;

    public StageProgressRepository(PracticeDbContext _context)
    {
        this._context = _context;
    }

    public async Task<StageProgress?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.StageProgresses
            .Include(s => s.AdaptiveAttempts)
            .Include(s => s.RoadmapNode)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<StageProgress?> GetByStudentAndNodeAsync(Guid studentId, Guid roadmapNodeId, CancellationToken ct = default)
    {
        return await _context.StageProgresses
            .Include(s => s.AdaptiveAttempts)
            .Include(s => s.RoadmapNode)
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.RoadmapNodeId == roadmapNodeId, ct);
    }

    public async Task AddAsync(StageProgress progress, CancellationToken ct = default)
    {
        await _context.StageProgresses.AddAsync(progress, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StageProgress progress, CancellationToken ct = default)
    {
        progress.UpdatedAt = DateTime.UtcNow;
        if (_context.Entry(progress).State == EntityState.Detached)
        {
            _context.StageProgresses.Attach(progress);
        }
        _context.Entry(progress).State = EntityState.Modified;
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddAttemptAsync(AdaptiveQuizAttempt attempt, CancellationToken ct = default)
    {
        await _context.AdaptiveQuizAttempts.AddAsync(attempt, ct);
        await _context.SaveChangesAsync(ct);
    }
}
