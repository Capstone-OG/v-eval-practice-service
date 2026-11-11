using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;

namespace V_Eval_Practice_Service.Infrastructure.Caching;

public class ClassHeatmapCacheService : IClassHeatmapCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ClassHeatmapCacheService> _logger;
    private static readonly ConcurrentDictionary<string, byte> _keys = new();

    public ClassHeatmapCacheService(IMemoryCache memoryCache, ILogger<ClassHeatmapCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("[Cache Hit] Khóa {Key} tìm thấy trong bộ đệm.", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("[Cache Miss] Khóa {Key} không có trong bộ đệm.", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };

        options.RegisterPostEvictionCallback((k, _, _, _) =>
        {
            _keys.TryRemove(k.ToString() ?? string.Empty, out _);
        });

        _memoryCache.Set(key, value, options);
        _keys.TryAdd(key, 0);

        _logger.LogDebug("[Cache Set] Đã lưu khóa {Key} với thời hạn {Expiration} phút.", key, expiration.TotalMinutes);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _memoryCache.Remove(key);
        _keys.TryRemove(key, out _);
        _logger.LogDebug("[Cache Invalidate] Đã xóa khóa {Key}.", key);
        return Task.CompletedTask;
    }

    public Task InvalidateClassHeatmapAsync(Guid classId, CancellationToken ct = default)
    {
        var prefix = $"heatmap:class:{classId}";
        var summaryKey = $"heatmap:summary:{classId}";

        RemoveAsync(prefix, ct);
        RemoveAsync(summaryKey, ct);

        // Xóa bất kỳ biến thể nào của lớp này (ví dụ theo domain)
        var relatedKeys = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var k in relatedKeys)
        {
            RemoveAsync(k, ct);
        }

        _logger.LogInformation("[Cache Invalidate] Đã xóa toàn bộ bộ đệm ma trận nhiệt của lớp {ClassId}.", classId);
        return Task.CompletedTask;
    }
}
