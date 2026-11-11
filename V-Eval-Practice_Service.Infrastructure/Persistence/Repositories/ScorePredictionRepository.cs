using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class ScorePredictionRepository : IScorePredictionRepository
{
    private readonly PracticeDbContext _context;

    public ScorePredictionRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<ScorePrediction?> GetLatestByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.ScorePredictions
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ScorePrediction?> GetByIdAsync(Guid predictionId, CancellationToken ct = default)
    {
        return await _context.ScorePredictions
            .FirstOrDefaultAsync(p => p.PredictionId == predictionId, ct);
    }

    public async Task<List<ScorePrediction>> GetHistoryByStudentIdAsync(Guid studentId, int count = 5, CancellationToken ct = default)
    {
        return await _context.ScorePredictions
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ScorePrediction prediction, CancellationToken ct = default)
    {
        await _context.ScorePredictions.AddAsync(prediction, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ScorePrediction prediction, CancellationToken ct = default)
    {
        _context.ScorePredictions.Update(prediction);
        await _context.SaveChangesAsync(ct);
    }
}
