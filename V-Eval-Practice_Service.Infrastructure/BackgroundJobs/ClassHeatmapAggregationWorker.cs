using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;

namespace V_Eval_Practice_Service.Infrastructure.BackgroundJobs;

/// <summary>
/// Background Job chạy định kỳ mỗi 15 phút để tính toán lại thống kê vĩ mô (Mean, StdDev, Alert Flags) cho toàn bộ lớp học (Core Flow 5)
/// </summary>
public class ClassHeatmapAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClassHeatmapAggregationWorker> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15);

    public ClassHeatmapAggregationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ClassHeatmapAggregationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Background Worker] ClassHeatmapAggregationWorker đã khởi chạy, chu kỳ quét: {Minutes} phút/lần.", _period.TotalMinutes);

        // Chờ 20 giây sau khi service khởi động xong mới kích hoạt đợt quét đầu tiên
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[Background Worker] Bắt đầu chu kỳ quét tổng hợp dữ liệu Class Heatmap...");

                using var scope = _scopeFactory.CreateScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<IClassHeatmapAggregatorService>();

                await aggregator.AggregateAllActiveClassesAsync(stoppingToken);

                _logger.LogInformation("[Background Worker] Chu kỳ quét thành công. Đợi đợt tiếp theo sau {Minutes} phút.", _period.TotalMinutes);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[Background Worker] Nhận tín hiệu dừng dịch vụ, kết thúc vòng lặp.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Background Worker] Gặp ngoại lệ khi thực hiện quét tổng hợp ma trận nhiệt.");
            }

            try
            {
                await Task.Delay(_period, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
