using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản trị dữ liệu ma trận nhiệt (Class Heatmap) và các chỉ số tổng hợp cấp lớp học (Core Flow 5)
/// </summary>
public interface IClassHeatmapRepository
{
    Task<List<ClassSkillHeatmap>> GetHeatmapsByClassIdAsync(Guid classId, string? domainCode = null, CancellationToken ct = default);
    Task<ClassSkillHeatmap?> GetHeatmapCellAsync(Guid classId, Guid studentId, Guid skillId, CancellationToken ct = default);
    Task UpsertHeatmapCellAsync(ClassSkillHeatmap heatmap, CancellationToken ct = default);
    Task BatchUpsertHeatmapCellsAsync(IEnumerable<ClassSkillHeatmap> heatmaps, CancellationToken ct = default);

    Task<List<ClassHeatmapSummary>> GetSummariesByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task UpsertSummaryAsync(ClassHeatmapSummary summary, CancellationToken ct = default);
    Task BatchUpsertSummariesAsync(IEnumerable<ClassHeatmapSummary> summaries, CancellationToken ct = default);

    Task<List<Guid>> GetActiveClassesAsync(CancellationToken ct = default);
    Task<int> GetClassStudentCountAsync(Guid classId, CancellationToken ct = default);
    Task<List<ClassEnrollment>> GetClassEnrollmentsAsync(Guid classId, CancellationToken ct = default);
    Task<Class?> GetClassByIdAsync(Guid classId, CancellationToken ct = default);
}
