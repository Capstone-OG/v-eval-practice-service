using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;

namespace V_Eval_Practice_Service.Infrastructure.BackgroundJobs;

/// <summary>
/// Background Worker chạy ngầm định kỳ (quét ban đêm hoặc mỗi 30 phút) để tự động phát hiện học sinh rủi ro theo quy tắc BR-04 và kích hoạt bắn thông báo Email & FCM (Checklist mục 07, SDD Figure 56 & 57)
/// </summary>
public class EarlyWarningSweepWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EarlyWarningSweepWorker> _logger;
    private readonly TimeSpan _period;

    public EarlyWarningSweepWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<EarlyWarningSweepWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        int intervalMinutes = int.TryParse(_configuration["EarlyWarning:SweepIntervalMinutes"], out var mins) && mins > 0
            ? mins
            : 30; // Mặc định quét chu kỳ 30 phút/lần
        _period = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[EarlyWarningSweepWorker] Worker cảnh báo sớm BR-04 đã khởi chạy. Chu kỳ quét tự động: {Minutes} phút/lần.",
            _period.TotalMinutes);

        // Chờ 25 giây sau khi ứng dụng khởi động xong để các dịch vụ liên kết (Identity gRPC) sẵn sàng
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[EarlyWarningSweepWorker] Bắt đầu đợt quét tự động học sinh có nguy cơ cảnh báo sớm BR-04...");

                using var scope = _scopeFactory.CreateScope();
                var sweepService = scope.ServiceProvider.GetRequiredService<IEarlyWarningSweepService>();

                var result = await sweepService.ExecuteSweepAsync(classId: null, stoppingToken);

                _logger.LogInformation(
                    "[EarlyWarningSweepWorker] Đợt quét hoàn tất thành công. Quét: {Scanned} học sinh | Sinh mới: {Alerts} cảnh báo | Bắn thông báo: {Dispatched}. Chu kỳ tiếp theo sau {Minutes} phút.",
                    result.TotalStudentsScanned, result.AlertsGenerated, result.NotificationsDispatched, _period.TotalMinutes);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[EarlyWarningSweepWorker] Nhận tín hiệu dừng dịch vụ, kết thúc vòng lặp Worker.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EarlyWarningSweepWorker] Gặp ngoại lệ khi thực hiện đợt quét cảnh báo sớm.");
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
