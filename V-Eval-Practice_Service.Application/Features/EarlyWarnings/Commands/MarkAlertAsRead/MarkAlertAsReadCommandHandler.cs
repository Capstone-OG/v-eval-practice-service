using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.MarkAlertAsRead;

public class MarkAlertAsReadCommandHandler : IRequestHandler<MarkAlertAsReadCommand, Result<MarkAlertAsReadResponseDto>>
{
    private readonly ISystemAlertRepository _alertRepository;

    public MarkAlertAsReadCommandHandler(ISystemAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<Result<MarkAlertAsReadResponseDto>> Handle(
        MarkAlertAsReadCommand command,
        CancellationToken cancellationToken)
    {
        if (command.StudentId == Guid.Empty)
        {
            return Result<MarkAlertAsReadResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (cần truyền header X-User-Id hoặc query param studentId)."));
        }

        if (command.AlertId == Guid.Empty)
        {
            return Result<MarkAlertAsReadResponseDto>.Failure(
                Error.Validation("Alert.InvalidId", "Mã thông báo alertId không hợp lệ."));
        }

        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken);
        if (alert == null)
        {
            return Result<MarkAlertAsReadResponseDto>.Failure(
                Error.NotFound("Alert.NotFound", $"Không tìm thấy thông báo cảnh báo với mã '{command.AlertId}'."));
        }

        if (alert.StudentId != command.StudentId)
        {
            return Result<MarkAlertAsReadResponseDto>.Failure(
                Error.Forbidden("Alert.Forbidden", "Bạn không có quyền thao tác trên thông báo cảnh báo của học sinh khác."));
        }

        if (!alert.IsRead)
        {
            alert.IsRead = true;
            await _alertRepository.UpdateAsync(alert, cancellationToken);
        }

        var response = new MarkAlertAsReadResponseDto
        {
            AlertId = alert.AlertId,
            StudentId = alert.StudentId,
            IsRead = true,
            ReadAt = DateTime.UtcNow,
            Message = "Đã đánh dấu thông báo cảnh báo thành công."
        };

        return Result<MarkAlertAsReadResponseDto>.Success(response);
    }
}
