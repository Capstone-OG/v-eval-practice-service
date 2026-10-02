using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản lý lộ trình học tập cá nhân hóa và các chặng học (Core Flow 2)
/// </summary>
public interface ILearningRoadmapRepository
{
    Task<LearningRoadmap?> GetByIdAsync(Guid roadmapId, CancellationToken ct = default);
    Task<LearningRoadmap?> GetActiveByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<IReadOnlyList<LearningRoadmap>> GetByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task AddAsync(LearningRoadmap roadmap, CancellationToken ct = default);
    Task ArchiveExistingActiveRoadmapsAsync(Guid studentId, CancellationToken ct = default);
    Task<LiveSession?> GetUpcomingLiveSessionAsync(Guid classId, CancellationToken ct = default);
    Task<Dictionary<string, LiveSession>> GetUpcomingThematicLiveSessionsAsync(Guid studentId, Guid? administrativeClassId, CancellationToken ct = default);
    Task<RoadmapNode?> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default);
    Task<RoadmapNode?> GetNextLockedNodeAsync(Guid roadmapId, int currentStepOrder, CancellationToken ct = default);
    Task<LiveSessionAttendance?> GetAttendanceAsync(Guid liveSessionId, Guid studentId, CancellationToken ct = default);
    Task AddAttendanceAsync(LiveSessionAttendance attendance, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
