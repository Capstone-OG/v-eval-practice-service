using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản lý nhật ký hoạt động chuyên cần, tính toán Streak và chỉ số sẵn sàng thi của học sinh (Core Flow 5)
/// </summary>
public interface IStudentActivityRepository
{
    Task<StudentDailyActivity?> GetDailyActivityAsync(Guid studentId, DateTime activityDate, CancellationToken ct = default);
    Task UpsertDailyActivityAsync(StudentDailyActivity activity, CancellationToken ct = default);
    Task<List<StudentDailyActivity>> GetRecentActivitiesAsync(Guid studentId, int days = 30, CancellationToken ct = default);
    Task<int> CalculateCurrentStreakAsync(Guid studentId, CancellationToken ct = default);
    Task<double> CalculateStudyHoursInPastDaysAsync(Guid studentId, int days = 7, CancellationToken ct = default);

    Task<StudentReadinessMetric?> GetReadinessMetricAsync(Guid studentId, CancellationToken ct = default);
    Task UpsertReadinessMetricAsync(StudentReadinessMetric metric, CancellationToken ct = default);
}
