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

public class LiveSessionRepository : ILiveSessionRepository
{
    private readonly PracticeDbContext _context;

    public LiveSessionRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<LiveSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.LiveSessions
            .Include(s => s.Class)
            .Include(s => s.Attendances)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
    }

    public async Task<Class?> GetClassByIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.Classes
            .FirstOrDefaultAsync(c => c.ClassId == classId, ct);
    }

    public async Task<IReadOnlyList<LiveSession>> GetSessionsByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.LiveSessions
            .Include(s => s.Class)
            .Where(s => s.ClassId == classId)
            .OrderBy(s => s.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LiveSession>> GetUpcomingSessionsForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        // 1. Tìm lớp học mà học sinh đang ghi danh
        var enrollment = await _context.ClassEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "ENROLLED")
            .OrderByDescending(e => e.EnrolledAt)
            .FirstOrDefaultAsync(ct);

        if (enrollment == null)
        {
            return Array.Empty<LiveSession>();
        }

        // 2. Lấy toàn bộ buổi Live của lớp học đó
        return await _context.LiveSessions
            .Include(s => s.Class)
            .Include(s => s.Attendances.Where(a => a.StudentId == studentId))
            .Where(s => s.ClassId == enrollment.ClassId)
            .OrderBy(s => s.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(LiveSession session, CancellationToken ct = default)
    {
        await _context.LiveSessions.AddAsync(session, ct);
    }

    public async Task<LiveSessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid studentId, CancellationToken ct = default)
    {
        return await _context.LiveSessionAttendances
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentId == studentId, ct);
    }

    public async Task AddAttendanceAsync(LiveSessionAttendance attendance, CancellationToken ct = default)
    {
        await _context.LiveSessionAttendances.AddAsync(attendance, ct);
    }

    public async Task<IReadOnlyList<LiveSessionAttendance>> GetAttendancesBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.LiveSessionAttendances
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LiveSession>> GetSessionsForTeacherAsync(Guid teacherId, CancellationToken ct = default)
    {
        return await _context.LiveSessions
            .Include(s => s.Class)
            .Include(s => s.Attendances)
            .Where(s => s.TeacherId == teacherId || (s.Class != null && s.Class.TeacherId == teacherId))
            .OrderBy(s => s.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetEnrolledStudentCountByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await _context.ClassEnrollments
            .CountAsync(e => e.ClassId == classId && e.Status == "ENROLLED", ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
