using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Quản lý truy xuất và cập nhật cảnh báo sớm 3 cấp BR-04 (Core Flow 6)
/// </summary>
public interface ISystemAlertRepository
{
    Task<SystemAlert?> GetByIdAsync(Guid alertId, CancellationToken ct = default);
    Task<List<SystemAlert>> GetByStudentIdAsync(Guid studentId, bool? unreadOnly = false, string? alertType = null, CancellationToken ct = default);
    Task<List<SystemAlert>> GetActiveAlertsByClassAsync(Guid classId, string? alertType = null, CancellationToken ct = default);
    Task AddAsync(SystemAlert alert, CancellationToken ct = default);
    Task UpdateAsync(SystemAlert alert, CancellationToken ct = default);
}
