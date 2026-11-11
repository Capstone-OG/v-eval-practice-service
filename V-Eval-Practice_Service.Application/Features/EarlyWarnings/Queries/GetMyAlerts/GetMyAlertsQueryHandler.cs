using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetMyAlerts;

public class GetMyAlertsQueryHandler : IRequestHandler<GetMyAlertsQuery, Result<MyAlertsResponseDto>>
{
    private readonly ISystemAlertRepository _alertRepository;

    public GetMyAlertsQueryHandler(ISystemAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<Result<MyAlertsResponseDto>> Handle(
        GetMyAlertsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.StudentId == Guid.Empty)
        {
            return Result<MyAlertsResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc query param studentId)."));
        }

        // Lấy toàn bộ cảnh báo của học sinh để thống kê số liệu tổng quan
        var allAlerts = await _alertRepository.GetByStudentIdAsync(
            query.StudentId,
            unreadOnly: false,
            alertType: null,
            ct: cancellationToken);

        // Áp dụng bộ lọc unreadOnly và alertType nếu người dùng yêu cầu
        var filtered = allAlerts.AsEnumerable();
        if (query.UnreadOnly == true)
        {
            filtered = filtered.Where(a => !a.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(query.AlertType))
        {
            var normType = query.AlertType.Trim().ToUpperInvariant();
            filtered = filtered.Where(a => a.AlertType.Equals(normType, StringComparison.OrdinalIgnoreCase));
        }

        var alertDtos = filtered.Select(a => new SystemAlertDto
        {
            AlertId = a.AlertId,
            StudentId = a.StudentId,
            AlertType = a.AlertType,
            Message = a.Message,
            IsRead = a.IsRead,
            HandledBy = a.HandledBy,
            ResolutionNotes = a.ResolutionNotes,
            ResolvedAt = a.ResolvedAt,
            CreatedAt = a.CreatedAt
        }).ToList();

        var response = new MyAlertsResponseDto
        {
            StudentId = query.StudentId,
            TotalAlerts = allAlerts.Count,
            UnreadCount = allAlerts.Count(a => !a.IsRead),
            RedAlertsCount = allAlerts.Count(a => a.AlertType.Equals("RED", StringComparison.OrdinalIgnoreCase)),
            YellowAlertsCount = allAlerts.Count(a => a.AlertType.Equals("YELLOW", StringComparison.OrdinalIgnoreCase)),
            Alerts = alertDtos
        };

        return Result<MyAlertsResponseDto>.Success(response);
    }
}
