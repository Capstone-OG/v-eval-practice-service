using System;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

/// <summary>
/// Dịch vụ đệm bộ nhớ (Memory/Redis Cache) phục vụ tối ưu hóa truy vấn ma trận nhiệt Class Heatmap dưới 300ms (Core Flow 5)
/// </summary>
public interface IClassHeatmapCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task InvalidateClassHeatmapAsync(Guid classId, CancellationToken ct = default);
}
