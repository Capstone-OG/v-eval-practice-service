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

    public async Task<Dictionary<string, LiveSession>> GetUpcomingThematicLiveSessionsAsync(
        Guid studentId,
        Guid? administrativeClassId,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, LiveSession>(StringComparer.OrdinalIgnoreCase);

        // 1. Lấy danh sách lớp chuyên đề (ClassType = 1) mà học sinh đã ghi danh
        var studentThematicClasses = await _context.ClassEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "ENROLLED")
            .Select(e => e.Class)
            .Where(c => c != null && c.ClassType == 1 && !string.IsNullOrEmpty(c.DomainCode))
            .ToListAsync(ct);

        if (studentThematicClasses.Count > 0)
        {
            var classIds = studentThematicClasses.Select(c => c!.ClassId).ToList();
            var sessions = await _context.LiveSessions
                .Include(ls => ls.Class)
                .Where(ls => classIds.Contains(ls.ClassId) && ls.Status == "SCHEDULED")
                .OrderBy(ls => ls.ScheduledAt)
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                var domainCode = session.Class?.DomainCode;
                if (!string.IsNullOrEmpty(domainCode) && !result.ContainsKey(domainCode))
                {
                    result[domainCode] = session;
                }
            }
        }

        // 2. Tìm campusId để tìm thêm lớp chuyên đề tại cơ sở (fallback nếu học sinh chưa ghi danh đủ các miền)
        Guid? campusId = null;
        if (administrativeClassId.HasValue)
        {
            var adminClass = await _context.Classes.FirstOrDefaultAsync(c => c.ClassId == administrativeClassId.Value, ct);
            campusId = adminClass?.CampusId;
        }
        else if (studentThematicClasses.Count > 0)
        {
            campusId = studentThematicClasses.First()?.CampusId;
        }

        if (campusId.HasValue)
        {
            var campusThematicSessions = await _context.LiveSessions
                .Include(ls => ls.Class)
                .Where(ls => ls.Class.CampusId == campusId.Value && ls.Class.ClassType == 1 && ls.Status == "SCHEDULED")
                .OrderBy(ls => ls.ScheduledAt)
                .ToListAsync(ct);

            foreach (var session in campusThematicSessions)
            {
                var domainCode = session.Class?.DomainCode;
                if (!string.IsNullOrEmpty(domainCode) && !result.ContainsKey(domainCode))
                {
                    result[domainCode] = session;
                }
            }
        }

        // 3. Fallback: Lớp hành chính chung (nếu có domain nào chưa có LiveSession chuyên đề)
        if (administrativeClassId.HasValue)
        {
            var adminSession = await _context.LiveSessions
                .Where(ls => ls.ClassId == administrativeClassId.Value && ls.Status == "SCHEDULED")
                .OrderBy(ls => ls.ScheduledAt)
                .FirstOrDefaultAsync(ct);

            if (adminSession != null)
            {
                result["DEFAULT"] = adminSession;
            }
        }

        return result;
    }

    public async Task<RoadmapNode?> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        return await _context.RoadmapNodes
            .Include(n => n.Roadmap)
            .Include(n => n.LiveSession)
            .FirstOrDefaultAsync(n => n.NodeId == nodeId, ct);
    }

    public async Task<RoadmapNode?> GetNextLockedNodeAsync(Guid roadmapId, int currentStepOrder, CancellationToken ct = default)
    {
        return await _context.RoadmapNodes
            .Where(n => n.RoadmapId == roadmapId && n.StepOrder > currentStepOrder && !n.IsPruned && n.Status == "LOCKED")
            .OrderBy(n => n.StepOrder)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LiveSessionAttendance?> GetAttendanceAsync(Guid liveSessionId, Guid studentId, CancellationToken ct = default)
    {
        return await _context.LiveSessionAttendances
            .FirstOrDefaultAsync(a => a.SessionId == liveSessionId && a.StudentId == studentId, ct);
    }

    public async Task AddAttendanceAsync(LiveSessionAttendance attendance, CancellationToken ct = default)
    {
        await _context.LiveSessionAttendances.AddAsync(attendance, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
