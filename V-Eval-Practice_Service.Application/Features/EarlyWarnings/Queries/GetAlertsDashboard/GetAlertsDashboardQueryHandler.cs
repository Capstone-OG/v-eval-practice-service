using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetAlertsDashboard;

public class GetAlertsDashboardQueryHandler : IRequestHandler<GetAlertsDashboardQuery, Result<EarlyWarningsDashboardResponseDto>>
{
    private readonly ISystemAlertRepository _alertRepository;
    private readonly IClassEnrollmentRepository _classEnrollmentRepository;
    private readonly IScorePredictionRepository _predictionRepository;
    private readonly IIdentityGrpcClient _identityGrpcClient;

    public GetAlertsDashboardQueryHandler(
        ISystemAlertRepository alertRepository,
        IClassEnrollmentRepository classEnrollmentRepository,
        IScorePredictionRepository predictionRepository,
        IIdentityGrpcClient identityGrpcClient)
    {
        _alertRepository = alertRepository;
        _classEnrollmentRepository = classEnrollmentRepository;
        _predictionRepository = predictionRepository;
        _identityGrpcClient = identityGrpcClient;
    }

    public async Task<Result<EarlyWarningsDashboardResponseDto>> Handle(
        GetAlertsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        Guid? effectiveClassId = query.ClassId;
        string className = "Tất cả học sinh";

        // 1. Xác định lớp học cần tra cứu nếu có
        if (effectiveClassId.HasValue && effectiveClassId.Value != Guid.Empty)
        {
            var targetClass = await _classEnrollmentRepository.GetClassByIdAsync(effectiveClassId.Value, cancellationToken);
            if (targetClass != null)
            {
                className = targetClass.Name;
            }
        }

        // 2. Truy xuất danh sách cảnh báo
        List<Domain.Entities.SystemAlert> allAlerts;
        if (effectiveClassId.HasValue && effectiveClassId.Value != Guid.Empty)
        {
            allAlerts = await _alertRepository.GetAlertsByClassAsync(
                effectiveClassId.Value,
                alertType: null,
                isResolved: null,
                ct: cancellationToken);
        }
        else
        {
            // Nếu không giới hạn theo lớp, lấy toàn bộ cảnh báo trong hệ thống
            allAlerts = await _alertRepository.GetAllAlertsAsync(
                alertType: null,
                isResolved: null,
                ct: cancellationToken);
        }

        // 2b. Tự động nâng cấp cảnh báo Cấp 2 (YELLOW) tồn đọng quá 7 ngày chưa xử lý lên Cấp 3 (RED - TIER_3_CRITICAL) theo MGR_19 / BR-24
        var escalationCutoff = DateTime.UtcNow.AddDays(-7);
        foreach (var staleAlert in allAlerts.Where(a =>
                     a.ResolvedAt == null &&
                     a.AlertType.Equals("YELLOW", StringComparison.OrdinalIgnoreCase) &&
                     a.CreatedAt <= escalationCutoff))
        {
            staleAlert.AlertType = "RED";
            if (!staleAlert.Message.Contains("[TIER_3_CRITICAL", StringComparison.OrdinalIgnoreCase))
            {
                staleAlert.Message = $"[TIER_3_CRITICAL - Tự động nâng cấp do quá hạn 7 ngày] {staleAlert.Message}";
            }
            await _alertRepository.UpdateAsync(staleAlert, cancellationToken);
        }

        // 3. Áp dụng bộ lọc IsResolved và AlertType cho danh sách chi tiết
        var filtered = allAlerts.AsEnumerable();
        if (query.IsResolved == true)
        {
            filtered = filtered.Where(a => a.ResolvedAt != null);
        }
        else if (query.IsResolved == false)
        {
            filtered = filtered.Where(a => a.ResolvedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(query.AlertType))
        {
            var normType = query.AlertType.Trim().ToUpperInvariant();
            filtered = filtered.Where(a => a.AlertType.Equals(normType, StringComparison.OrdinalIgnoreCase));
        }

        // 3b. Khử trùng lặp: Mỗi học sinh chỉ giữ lại 1 cảnh báo mới nhất cho mỗi phân loại cảnh báo
        var filteredList = filtered
            .GroupBy(a => new { a.StudentId, a.AlertType })
            .Select(g => g.OrderByDescending(a => a.CreatedAt).First())
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var studentIds = filteredList.Select(a => a.StudentId).Distinct().ToList();

        // 4. Lấy điểm dự báo mới nhất của từng học sinh để bổ sung ngữ cảnh cho Mentor
        var predictionsDict = new Dictionary<Guid, Domain.Entities.ScorePrediction>();
        foreach (var sid in studentIds)
        {
            var p = await _predictionRepository.GetLatestByStudentIdAsync(sid, cancellationToken);
            if (p != null)
            {
                predictionsDict[sid] = p;
            }
        }

        // 4b. Truy vấn họ tên học sinh thực tế từ Identity Service qua gRPC (Không hardcode)
        var studentNamesDict = await _identityGrpcClient.GetStudentNamesAsync(studentIds, cancellationToken);

        var alertItems = filteredList.Select(a =>
        {
            predictionsDict.TryGetValue(a.StudentId, out var p);
            var studentName = studentNamesDict.TryGetValue(a.StudentId, out var resolvedName) && !string.IsNullOrWhiteSpace(resolvedName)
                ? resolvedName
                : $"Học sinh {a.StudentId.ToString()[..8].ToUpperInvariant()}";

            return new AlertDashboardItemDto
            {
                AlertId = a.AlertId,
                StudentId = a.StudentId,
                StudentName = studentName,
                AlertType = a.AlertType,
                Message = a.Message,
                IsRead = a.IsRead,
                CurrentPredictedScore = p?.PredictedScore,
                TargetScore = p?.TargetScore,
                DeficitScore = p?.DeficitScore,
                HandledBy = a.HandledBy,
                ResolutionNotes = a.ResolutionNotes,
                ResolvedAt = a.ResolvedAt,
                CreatedAt = a.CreatedAt
            };
        }).ToList();

        var response = new EarlyWarningsDashboardResponseDto
        {
            ClassId = effectiveClassId,
            ClassName = className,
            TotalAlerts = allAlerts.Count,
            ActiveAlertsCount = allAlerts.Count(a => a.ResolvedAt == null),
            RedAlertsCount = allAlerts.Count(a => a.AlertType.Equals("RED", StringComparison.OrdinalIgnoreCase)),
            YellowAlertsCount = allAlerts.Count(a => a.AlertType.Equals("YELLOW", StringComparison.OrdinalIgnoreCase)),
            UnrealisticGoalCount = allAlerts.Count(a => a.AlertType.Equals("UNREALISTIC_GOAL", StringComparison.OrdinalIgnoreCase)),
            ResolvedAlertsCount = allAlerts.Count(a => a.ResolvedAt != null),
            Alerts = alertItems
        };

        return Result<EarlyWarningsDashboardResponseDto>.Success(response);
    }
}
