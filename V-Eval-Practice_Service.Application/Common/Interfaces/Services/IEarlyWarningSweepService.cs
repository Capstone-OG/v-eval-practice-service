using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

/// <summary>
/// DTO tóm tắt kết quả chu kỳ quét cảnh báo sớm tự động (Early Warning Sweep)
/// </summary>
public record EarlyWarningSweepResultDto(
    int TotalStudentsScanned,
    int AlertsGenerated,
    int NotificationsDispatched,
    List<string> GeneratedAlertSummaries,
    DateTime ExecutedAt
);

/// <summary>
/// Giao diện dịch vụ quét tự động phát hiện rủi ro học tập theo quy tắc BR-04 và kích hoạt điều phối thông báo (Checklist mục 07, SDD Figure 56 & 57)
/// </summary>
public interface IEarlyWarningSweepService
{
    /// <summary>
    /// Thực hiện quét toàn bộ học sinh đang hoạt động (hoặc theo lớp chỉ định), kiểm tra ngưỡng quy tắc BR-04, lưu SystemAlert và bắn thông báo
    /// </summary>
    Task<EarlyWarningSweepResultDto> ExecuteSweepAsync(Guid? classId = null, CancellationToken ct = default);
}
