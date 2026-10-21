using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Quản lý truy xuất và lưu trữ dữ liệu dự báo điểm số và toa thuốc Knapsack ROI (Core Flow 6)
/// </summary>
public interface IScorePredictionRepository
{
    Task<ScorePrediction?> GetLatestByStudentIdAsync(Guid studentId, CancellationToken ct = default);
    Task<ScorePrediction?> GetByIdAsync(Guid predictionId, CancellationToken ct = default);
    Task<List<ScorePrediction>> GetHistoryByStudentIdAsync(Guid studentId, int count = 5, CancellationToken ct = default);
    Task AddAsync(ScorePrediction prediction, CancellationToken ct = default);
    Task UpdateAsync(ScorePrediction prediction, CancellationToken ct = default);
}
