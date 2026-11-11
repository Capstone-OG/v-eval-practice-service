using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ResolveAlert;

public class ResolveAlertCommandHandler : IRequestHandler<ResolveAlertCommand, Result<ResolveAlertResponseDto>>
{
    private readonly ISystemAlertRepository _alertRepository;

    public ResolveAlertCommandHandler(ISystemAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<Result<ResolveAlertResponseDto>> Handle(
        ResolveAlertCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AlertId == Guid.Empty)
        {
            return Result<ResolveAlertResponseDto>.Failure(
                Error.Validation("Alert.InvalidId", "Mã thông báo alertId không hợp lệ."));
        }

        Guid mentorId = command.Request?.MentorId.HasValue == true && command.Request.MentorId.Value != Guid.Empty
            ? command.Request.MentorId.Value
            : command.EffectiveMentorId;

        if (mentorId == Guid.Empty)
        {
            return Result<ResolveAlertResponseDto>.Failure(
                Error.Unauthorized("Auth.MentorIdRequired", "Không tìm thấy định danh Mentor/Giáo viên (cần truyền header X-User-Id hoặc trường mentor_id)."));
        }

        if (string.IsNullOrWhiteSpace(command.Request?.ResolutionNotes))
        {
            return Result<ResolveAlertResponseDto>.Failure(
                Error.Validation("Alert.NotesRequired", "Ghi chú can thiệp sư phạm (resolution_notes) không được để trống."));
        }

        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken);
        if (alert == null)
        {
            return Result<ResolveAlertResponseDto>.Failure(
                Error.NotFound("Alert.NotFound", $"Không tìm thấy thông báo cảnh báo với mã '{command.AlertId}'."));
        }

        alert.HandledBy = mentorId;
        alert.ResolutionNotes = command.Request.ResolutionNotes.Trim();
        alert.ResolvedAt = DateTime.UtcNow;
        alert.IsRead = true;

        await _alertRepository.UpdateAsync(alert, cancellationToken);

        var response = new ResolveAlertResponseDto
        {
            AlertId = alert.AlertId,
            StudentId = alert.StudentId,
            AlertType = alert.AlertType,
            IsResolved = true,
            HandledBy = alert.HandledBy,
            ResolutionNotes = alert.ResolutionNotes,
            ResolvedAt = alert.ResolvedAt,
            Message = "Đã ghi nhận can thiệp sư phạm và giải quyết cảnh báo thành công."
        };

        return Result<ResolveAlertResponseDto>.Success(response);
    }
}
