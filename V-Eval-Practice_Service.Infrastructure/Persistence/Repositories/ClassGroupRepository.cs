using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class ClassGroupRepository : IClassGroupRepository
{
    private readonly PracticeDbContext _context;
    private readonly ILogger<ClassGroupRepository> _logger;

    public ClassGroupRepository(PracticeDbContext context, ILogger<ClassGroupRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassGroup>> GetGroupsByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.ClassGroups
            .Include(g => g.Members)
            .Where(g => g.ClassId == classId)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);
    }

    public async Task<ClassGroup?> GetGroupByIdAsync(Guid groupId, CancellationToken ct = default)
    {
        return await _context.ClassGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);
    }

    public async Task CreateGroupsAsync(IEnumerable<ClassGroup> groups, CancellationToken ct = default)
    {
        await _context.ClassGroups.AddRangeAsync(groups, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteGroupsByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        var existingGroups = await _context.ClassGroups
            .Where(g => g.ClassId == classId)
            .ToListAsync(ct);

        if (existingGroups.Count > 0)
        {
            _context.ClassGroups.RemoveRange(existingGroups);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Deleted {Count} old micro-groups for class {ClassId}", existingGroups.Count, classId);
        }
    }

    public async Task<bool> AssignWorksheetAsync(
        Guid groupId,
        string worksheetId,
        string worksheetTitle,
        CancellationToken ct = default)
    {
        var group = await _context.ClassGroups.FirstOrDefaultAsync(g => g.GroupId == groupId, ct);
        if (group == null)
            return false;

        group.AssignedWorksheetId = worksheetId;
        group.AssignedWorksheetTitle = worksheetTitle;
        group.WorksheetAssignedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Assigned worksheet '{WorksheetTitle}' ({WorksheetId}) to micro-group {GroupId}",
            worksheetTitle, worksheetId, groupId);
        return true;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
