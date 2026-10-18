using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class SystemAlertRepository : ISystemAlertRepository
{
    private readonly PracticeDbContext _context;

    public SystemAlertRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<SystemAlert?> GetByIdAsync(Guid alertId, CancellationToken ct = default)
    {
        return await _context.SystemAlerts
            .FirstOrDefaultAsync(a => a.AlertId == alertId, ct);
    }

    public async Task<List<SystemAlert>> GetByStudentIdAsync(Guid studentId, bool? unreadOnly = false, CancellationToken ct = default)
    {
        var query = _context.SystemAlerts.Where(a => a.StudentId == studentId);
        if (unreadOnly == true)
        {
            query = query.Where(a => !a.IsRead);
        }
        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }

    public async Task<List<SystemAlert>> GetActiveAlertsByClassAsync(Guid classId, string? alertType = null, CancellationToken ct = default)
    {
        // Join với ClassEnrollments để lấy danh sách học sinh thuộc lớp
        var studentIds = await _context.ClassEnrollments
            .Where(ce => ce.ClassId == classId)
            .Select(ce => ce.StudentId)
            .ToListAsync(ct);

        var query = _context.SystemAlerts
            .Where(a => studentIds.Contains(a.StudentId) && a.ResolvedAt == null);

        if (!string.IsNullOrEmpty(alertType))
        {
            query = query.Where(a => a.AlertType == alertType);
        }

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(SystemAlert alert, CancellationToken ct = default)
    {
        await _context.SystemAlerts.AddAsync(alert, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SystemAlert alert, CancellationToken ct = default)
    {
        _context.SystemAlerts.Update(alert);
        await _context.SaveChangesAsync(ct);
    }
}
