using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản lý buổi học trực tuyến Live Q&A, lịch học và điểm danh (Core Flow 2 - Phase 3)
/// </summary>
public interface ILiveSessionRepository
{
    Task<LiveSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<Class?> GetClassByIdAsync(Guid classId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveSession>> GetSessionsByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveSession>> GetUpcomingSessionsForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task AddAsync(LiveSession session, CancellationToken ct = default);
    Task<LiveSessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid studentId, CancellationToken ct = default);
    Task AddAttendanceAsync(LiveSessionAttendance attendance, CancellationToken ct = default);
    Task<IReadOnlyList<LiveSessionAttendance>> GetAttendancesBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveSession>> GetSessionsForTeacherAsync(Guid teacherId, CancellationToken ct = default);
    Task<bool> TeacherExistsAsync(Guid teacherId, CancellationToken ct = default);
    Task<int> GetEnrolledStudentCountByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
