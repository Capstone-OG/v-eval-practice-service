using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

public interface IClassGroupRepository
{
    Task<IReadOnlyList<ClassGroup>> GetGroupsByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<ClassGroup?> GetGroupByIdAsync(Guid groupId, CancellationToken ct = default);
    Task CreateGroupsAsync(IEnumerable<ClassGroup> groups, CancellationToken ct = default);
    Task DeleteGroupsByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<bool> AssignWorksheetAsync(Guid groupId, string worksheetId, string worksheetTitle, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
