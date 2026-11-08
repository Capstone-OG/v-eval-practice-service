using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Services;

/// <summary>
/// Dịch vụ quét tự động phát hiện rủi ro học tập theo quy tắc BR-04 và kích hoạt điều phối thông báo đa kênh (Checklist mục 07, SDD Figure 56 & 57)
/// </summary>
public class EarlyWarningSweepService : IEarlyWarningSweepService
{
    private readonly PracticeDbContext _context;
    private readonly ISystemAlertRepository _alertRepository;
    private readonly IStudentActivityRepository _activityRepository;
    private readonly IScorePredictionRepository _predictionRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<EarlyWarningSweepService> _logger;

    public EarlyWarningSweepService(
        PracticeDbContext context,
        ISystemAlertRepository alertRepository,
        IStudentActivityRepository activityRepository,
        IScorePredictionRepository predictionRepository,
        IIdentityGrpcClient identityGrpcClient,
        INotificationDispatcher notificationDispatcher,
        ILogger<EarlyWarningSweepService> logger)
    {
        _context = context;
        _alertRepository = alertRepository;
        _activityRepository = activityRepository;
        _predictionRepository = predictionRepository;
        _identityGrpcClient = identityGrpcClient;
        _notificationDispatcher = notificationDispatcher;
        _logger = logger;
    }

    public async Task<EarlyWarningSweepResultDto> ExecuteSweepAsync(Guid? classId = null, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("[EarlyWarningSweepService] Bắt đầu chu kỳ quét cảnh báo sớm BR-04 (ClassId: {ClassId}) lúc {Time}",
            classId?.ToString() ?? "ALL", startTime);

        // 1. Thu thập danh sách học sinh cần quét
        List<Guid> studentIds;
        if (classId.HasValue && classId.Value != Guid.Empty)
        {
            studentIds = await _context.ClassEnrollments
                .AsNoTracking()
                .Where(e => e.ClassId == classId.Value)
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(ct);
        }
        else
        {
            var enrolledStudents = await _context.ClassEnrollments
                .AsNoTracking()
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(ct);

            var activeMetricStudents = await _context.StudentReadinessMetrics
                .AsNoTracking()
                .Select(m => m.StudentId)
                .Distinct()
                .ToListAsync(ct);

            var roadmapStudents = await _context.LearningRoadmaps
                .AsNoTracking()
                .Select(r => r.StudentId)
                .Distinct()
                .ToListAsync(ct);

            studentIds = enrolledStudents
                .Union(activeMetricStudents)
                .Union(roadmapStudents)
                .Distinct()
                .ToList();
        }

        if (studentIds.Count == 0)
        {
            _logger.LogInformation("[EarlyWarningSweepService] Không có học sinh nào cần quét trong phạm vi chỉ định.");
            return new EarlyWarningSweepResultDto(0, 0, 0, new List<string>(), DateTime.UtcNow);
        }

        int alertsGeneratedCount = 0;
        int notificationsDispatchedCount = 0;
        var alertSummaries = new List<string>();

        // 2. Duyệt qua từng học sinh và đánh giá theo quy tắc BR-04
        foreach (var studentId in studentIds)
        {
            try
            {
                var profile = await _identityGrpcClient.GetStudentProfileAsync(studentId, ct);
                int targetScore = profile.TargetScore > 0 ? profile.TargetScore : 900;
                string campusName = profile.CampusName ?? "V-Eval Online";

                var readiness = await _activityRepository.GetReadinessMetricAsync(studentId, ct);
                double studyHours = await _activityRepository.CalculateStudyHoursInPastDaysAsync(studentId, 7, ct);
                int streak = await _activityRepository.CalculateCurrentStreakAsync(studentId, ct);

                var prediction = await _predictionRepository.GetLatestByStudentIdAsync(studentId, ct);
                int predictedScore = prediction?.PredictedScore ?? (readiness != null ? (int)Math.Round(readiness.NormalizedTheta * 12.0) : 600);
                int deficitScore = Math.Max(0, targetScore - predictedScore);

                double readinessIndex = readiness?.ExamReadinessIndex ?? 0.0;
                string readinessTier = readiness?.ReadinessTier ?? "PREPARING";

                // Lấy các cảnh báo chưa xử lý gần đây của học sinh này (trong vòng 24h) để chống sinh trùng lặp
                var existingAlerts = await _alertRepository.GetByStudentIdAsync(studentId, unreadOnly: false, alertType: null, ct);
                var recentUnresolved = existingAlerts
                    .Where(a => a.ResolvedAt == null && a.CreatedAt >= DateTime.UtcNow.AddHours(-24))
                    .ToList();

                SystemAlert? candidateAlert = null;

                // Quy tắc 1: Cảnh báo mục tiêu không tưởng (UNREALISTIC_GOAL)
                if (deficitScore >= 200 || (targetScore >= 1000 && predictedScore < 750))
                {
                    if (!recentUnresolved.Any(a => a.AlertType == "UNREALISTIC_GOAL"))
                    {
                        candidateAlert = new SystemAlert
                        {
                            AlertId = Guid.NewGuid(),
                            StudentId = studentId,
                            AlertType = "UNREALISTIC_GOAL",
                            Message = $"Cảnh báo mục tiêu không tưởng: Điểm dự báo hiện tại ({predictedScore}/1200) thâm hụt {deficitScore} điểm so với mục tiêu cam kết ({targetScore}/1200). Đề xuất tổ chức phiên tư vấn 3 bên (Phụ huynh - Học sinh - Mentor) để định hướng lại mục tiêu thực tế hơn.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }
                // Quy tắc 2: Cảnh báo nguy cơ cao (RED)
                else if (studyHours < 2.0 || (readiness != null && readinessIndex < 40.0))
                {
                    if (!recentUnresolved.Any(a => a.AlertType == "RED"))
                    {
                        candidateAlert = new SystemAlert
                        {
                            AlertId = Guid.NewGuid(),
                            StudentId = studentId,
                            AlertType = "RED",
                            Message = $"Cảnh báo nguy cơ cao (Mức Đỏ): Học sinh ghi nhận thời lượng tự học tuần chỉ đạt {studyHours:F1} giờ (dưới định mức tối thiểu 10h/tuần), chỉ số sẵn sàng thi {readinessIndex:F1}/100. Cần can thiệp sư phạm khẩn cấp từ Mentor.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }
                // Quy tắc 3: Cảnh báo nhắc nhở tiến độ (YELLOW)
                else if (studyHours < 6.0 || (readiness != null && readinessIndex < 60.0))
                {
                    if (!recentUnresolved.Any(a => a.AlertType == "YELLOW" || a.AlertType == "RED"))
                    {
                        candidateAlert = new SystemAlert
                        {
                            AlertId = Guid.NewGuid(),
                            StudentId = studentId,
                            AlertType = "YELLOW",
                            Message = $"Cảnh báo tiến độ (Mức Vàng): Thời lượng tự học tuần này chỉ đạt {studyHours:F1}h / 12h khuyến nghị. Đề nghị gia đình nhắc nhở và động viên con duy trì chuỗi học tập đều đặn.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }

                // Nếu phát sinh cảnh báo mới: lưu DB và bắn thông báo
                if (candidateAlert != null)
                {
                    await _alertRepository.AddAsync(candidateAlert, ct);
                    alertsGeneratedCount++;

                    // Thu thập thông tin phụ huynh liên kết để bắn email
                    var parentEmails = new List<string>();
                    var linkedParents = await _identityGrpcClient.GetParentStudentsAsync(Guid.Empty, ct);
                    // Lấy email phụ huynh nếu có
                    if (linkedParents != null)
                    {
                        foreach (var p in linkedParents)
                        {
                            if (p.StudentId == studentId && !string.IsNullOrWhiteSpace(p.Email))
                            {
                                parentEmails.Add(p.Email);
                            }
                        }
                    }

                    var studentNotificationInfo = new StudentAlertNotificationDto(
                        StudentId: studentId,
                        FullName: $"Học sinh {studentId.ToString()[..8].ToUpperInvariant()}",
                        Email: string.Empty, // Sẽ được gửi tới email phụ huynh
                        TargetScore: targetScore,
                        PredictedScore: predictedScore,
                        DeficitScore: deficitScore,
                        StudyHoursThisWeek: studyHours,
                        ReadinessIndex: readinessIndex,
                        ReadinessTier: readinessTier,
                        CurrentStreak: streak,
                        ParentEmails: parentEmails,
                        CampusName: campusName
                    );

                    var dispatchResult = await _notificationDispatcher.DispatchAlertAsync(candidateAlert, studentNotificationInfo, ct);
                    if (dispatchResult.EmailSent || dispatchResult.PushSent)
                    {
                        notificationsDispatchedCount++;
                    }

                    var summaryItem = $"[HS: {studentId.ToString()[..8].ToUpper()}] Alert: {candidateAlert.AlertType} | Thâm hụt: -{deficitScore}đ | Học: {studyHours:F1}h";
                    alertSummaries.Add(summaryItem);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[EarlyWarningSweepService] Gặp lỗi khi đánh giá học sinh {StudentId}: {Message}", studentId, ex.Message);
            }
        }

        _logger.LogInformation(
            "[EarlyWarningSweepService] Kết thúc chu kỳ quét. Quét {Scanned} học sinh, phát hiện {Alerts} cảnh báo mới, điều phối {Dispatched} thông báo.",
            studentIds.Count, alertsGeneratedCount, notificationsDispatchedCount);

        return new EarlyWarningSweepResultDto(
            TotalStudentsScanned: studentIds.Count,
            AlertsGenerated: alertsGeneratedCount,
            NotificationsDispatched: notificationsDispatchedCount,
            GeneratedAlertSummaries: alertSummaries,
            ExecutedAt: DateTime.UtcNow
        );
    }
}
