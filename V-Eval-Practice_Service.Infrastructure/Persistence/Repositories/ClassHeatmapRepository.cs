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

public class ClassHeatmapRepository : IClassHeatmapRepository
{
    private readonly PracticeDbContext _context;

    public ClassHeatmapRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClassSkillHeatmap>> GetHeatmapsByClassIdAsync(Guid classId, string? domainCode = null, CancellationToken ct = default)
    {
        var query = _context.ClassSkillHeatmaps
            .AsNoTracking()
            .Where(h => h.ClassId == classId);

        if (!string.IsNullOrWhiteSpace(domainCode))
        {
            query = query.Where(h => h.DomainCode == domainCode);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<List<ClassSkillHeatmap>> GetHeatmapsByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.ClassSkillHeatmaps
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .ToListAsync(ct);
    }

    public async Task<ClassSkillHeatmap?> GetHeatmapCellAsync(Guid classId, Guid studentId, Guid skillId, CancellationToken ct = default)
    {
        return await _context.ClassSkillHeatmaps
            .FirstOrDefaultAsync(h => h.ClassId == classId && h.StudentId == studentId && h.SkillId == skillId, ct);
    }

    public async Task UpsertHeatmapCellAsync(ClassSkillHeatmap heatmap, CancellationToken ct = default)
    {
        var existing = await _context.ClassSkillHeatmaps
            .FirstOrDefaultAsync(h => h.ClassId == heatmap.ClassId && h.StudentId == heatmap.StudentId && h.SkillId == heatmap.SkillId, ct);

        if (existing == null)
        {
            await _context.ClassSkillHeatmaps.AddAsync(heatmap, ct);
        }
        else
        {
            existing.PltValue = heatmap.PltValue;
            existing.MasteryLevel = heatmap.MasteryLevel;
            existing.LastAssessmentType = heatmap.LastAssessmentType;
            existing.SkillName = heatmap.SkillName;
            existing.DomainCode = heatmap.DomainCode;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task BatchUpsertHeatmapCellsAsync(IEnumerable<ClassSkillHeatmap> heatmaps, CancellationToken ct = default)
    {
        foreach (var item in heatmaps)
        {
            var existing = await _context.ClassSkillHeatmaps
                .FirstOrDefaultAsync(h => h.ClassId == item.ClassId && h.StudentId == item.StudentId && h.SkillId == item.SkillId, ct);

            if (existing == null)
            {
                await _context.ClassSkillHeatmaps.AddAsync(item, ct);
            }
            else
            {
                existing.PltValue = item.PltValue;
                existing.MasteryLevel = item.MasteryLevel;
                existing.LastAssessmentType = item.LastAssessmentType;
                existing.SkillName = item.SkillName;
                existing.DomainCode = item.DomainCode;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ClassHeatmapSummary>> GetSummariesByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.ClassHeatmapSummaries
            .AsNoTracking()
            .Where(s => s.ClassId == classId)
            .OrderByDescending(s => s.AlertStatus == "CRITICAL_RED")
            .ThenByDescending(s => s.AlertStatus == "WARNING_AMBER")
            .ThenBy(s => s.AveragePlt)
            .ToListAsync(ct);
    }

    public async Task UpsertSummaryAsync(ClassHeatmapSummary summary, CancellationToken ct = default)
    {
        var existing = await _context.ClassHeatmapSummaries
            .FirstOrDefaultAsync(s => s.ClassId == summary.ClassId && s.SkillId == summary.SkillId, ct);

        if (existing == null)
        {
            await _context.ClassHeatmapSummaries.AddAsync(summary, ct);
        }
        else
        {
            existing.AveragePlt = summary.AveragePlt;
            existing.StrugglingCount = summary.StrugglingCount;
            existing.TotalStudents = summary.TotalStudents;
            existing.StrugglingRate = summary.StrugglingRate;
            existing.StdDevPlt = summary.StdDevPlt;
            existing.AlertStatus = summary.AlertStatus;
            existing.IsBimodal = summary.IsBimodal;
            existing.CalculatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task BatchUpsertSummariesAsync(IEnumerable<ClassHeatmapSummary> summaries, CancellationToken ct = default)
    {
        foreach (var item in summaries)
        {
            var existing = await _context.ClassHeatmapSummaries
                .FirstOrDefaultAsync(s => s.ClassId == item.ClassId && s.SkillId == item.SkillId, ct);

            if (existing == null)
            {
                await _context.ClassHeatmapSummaries.AddAsync(item, ct);
            }
            else
            {
                existing.AveragePlt = item.AveragePlt;
                existing.StrugglingCount = item.StrugglingCount;
                existing.TotalStudents = item.TotalStudents;
                existing.StrugglingRate = item.StrugglingRate;
                existing.StdDevPlt = item.StdDevPlt;
                existing.AlertStatus = item.AlertStatus;
                existing.IsBimodal = item.IsBimodal;
                existing.CalculatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<Guid>> GetActiveClassesAsync(CancellationToken ct = default)
    {
        return await _context.Classes
            .AsNoTracking()
            .Where(c => c.Status == "ACTIVE")
            .Select(c => c.ClassId)
            .ToListAsync(ct);
    }

    public async Task<List<Class>> GetClassesByCampusIdAsync(Guid campusId, CancellationToken ct = default)
    {
        return await _context.Classes
            .AsNoTracking()
            .Where(c => c.CampusId == campusId && c.Status == "ACTIVE")
            .ToListAsync(ct);
    }

    public async Task<int> GetClassStudentCountAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .AsNoTracking()
            .CountAsync(e => e.ClassId == classId && e.Status == "ENROLLED", ct);
    }

    public async Task<List<ClassEnrollment>> GetClassEnrollmentsAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .AsNoTracking()
            .Where(e => e.ClassId == classId && e.Status == "ENROLLED")
            .ToListAsync(ct);
    }

    public async Task<Class?> GetClassByIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct);
    }
}
