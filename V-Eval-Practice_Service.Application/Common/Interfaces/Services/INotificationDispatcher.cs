using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

/// <summary>
/// DTO chứa thông tin học sinh và phụ huynh phục vụ bắn thông báo cảnh báo sớm ra bên ngoài (Email, Mobile Push FCM)
/// </summary>
public record StudentAlertNotificationDto(
    Guid StudentId,
    string FullName,
    string Email,
    int TargetScore,
    int PredictedScore,
    int DeficitScore,
    double StudyHoursThisWeek,
    double ReadinessIndex,
    string ReadinessTier,
    int CurrentStreak,
    List<string> ParentEmails,
    string? CampusName = null
);

/// <summary>
/// Kết quả điều phối thông báo đa kênh (Email SMTP & Mobile Push FCM)
/// </summary>
public record NotificationDispatchResult(
    bool EmailSent,
    int EmailRecipientCount,
    bool PushSent,
    int PushRecipientCount,
    string? StatusMessage = null
);

/// <summary>
/// Giao diện điều phối bắn thông báo cảnh báo sớm (BR-04) ra bên ngoài qua Email SMTP và Mobile Push FCM (Figure 56 & 57)
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Điều phối gửi thông báo đa kênh khi phát sinh cảnh báo sớm
    /// </summary>
    Task<NotificationDispatchResult> DispatchAlertAsync(
        SystemAlert alert,
        StudentAlertNotificationDto studentInfo,
        CancellationToken ct = default);
}
