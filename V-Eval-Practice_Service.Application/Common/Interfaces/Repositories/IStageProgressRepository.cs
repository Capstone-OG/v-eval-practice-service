using System;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface IStageProgressRepository
{
    Task<StageProgress?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StageProgress?> GetByStudentAndNodeAsync(Guid studentId, Guid roadmapNodeId, CancellationToken ct = default);
    Task AddAsync(StageProgress progress, CancellationToken ct = default);
    Task UpdateAsync(StageProgress progress, CancellationToken ct = default);
    Task AddAttemptAsync(AdaptiveQuizAttempt attempt, CancellationToken ct = default);
}
