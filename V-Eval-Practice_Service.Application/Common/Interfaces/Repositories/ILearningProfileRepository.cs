using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface ILearningProfileRepository
{
    Task UpsertSkillPriorsAsync(
        Guid studentId,
        IEnumerable<(Guid SkillId, double MasteryScore)> priors,
        CancellationToken ct = default);

    Task<IReadOnlyList<LearningProfile>> GetByStudentIdAsync(
        Guid studentId,
        CancellationToken ct = default);
}
