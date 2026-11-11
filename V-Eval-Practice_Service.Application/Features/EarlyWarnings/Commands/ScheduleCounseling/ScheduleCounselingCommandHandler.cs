using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ScheduleCounseling;

public class ScheduleCounselingCommandHandler : IRequestHandler<ScheduleCounselingCommand, Result<ScheduleCounselingResponseDto>>
{
    private readonly ISystemAlertRepository _alertRepository;

    public ScheduleCounselingCommandHandler(ISystemAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<Result<ScheduleCounselingResponseDto>> Handle(
        ScheduleCounselingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AlertId == Guid.Empty)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.Validation("Alert.InvalidId", "Mã thông báo alertId không hợp lệ."));
        }

        Guid mentorId = command.Request?.MentorId.HasValue == true && command.Request.MentorId.Value != Guid.Empty
            ? command.Request.MentorId.Value
            : command.EffectiveMentorId;

        if (mentorId == Guid.Empty)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.Unauthorized("Auth.MentorIdRequired", "Không tìm thấy định danh Mentor/Giáo viên (cần truyền header X-User-Id hoặc trường mentor_id)."));
        }

        if (command.Request == null)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.Validation("Counseling.PayloadRequired", "Dữ liệu yêu cầu hẹn lịch tư vấn không được để trống."));
        }

        if (command.Request.CounselingDate <= DateTime.UtcNow)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.Validation("Counseling.InvalidDate", "Thời gian hẹn tư vấn phải ở tương lai."));
        }

        if (command.Request.ProposedTargetScore <= 0 || command.Request.ProposedTargetScore > 1200)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.Validation("Counseling.InvalidTargetScore", "Điểm mục tiêu đề xuất phải nằm trong thang điểm ĐGNL từ 1 đến 1200."));
        }

        var alert = await _alertRepository.GetByIdAsync(command.AlertId, cancellationToken);
        if (alert == null)
        {
            return Result<ScheduleCounselingResponseDto>.Failure(
                Error.NotFound("Alert.NotFound", $"Không tìm thấy thông báo cảnh báo với mã '{command.AlertId}'."));
        }

        string location = string.IsNullOrWhiteSpace(command.Request.CounselingLocation)
            ? "Phòng Tư Vấn Học Đường - Cơ sở chính"
            : command.Request.CounselingLocation.Trim();

        string notes = command.Request.MentorNotes?.Trim() ?? string.Empty;

        alert.HandledBy = mentorId;
        alert.ResolutionNotes = $"[LỊCH TƯ VẤN] Ngày: {command.Request.CounselingDate:dd/MM/yyyy HH:mm} - Địa điểm: {location} - Điểm mục tiêu đề xuất: {command.Request.ProposedTargetScore} điểm. Ghi chú Mentor: {notes}";
        alert.ResolvedAt = DateTime.UtcNow;
        alert.IsRead = true;

        await _alertRepository.UpdateAsync(alert, cancellationToken);

        var response = new ScheduleCounselingResponseDto
        {
            AlertId = alert.AlertId,
            StudentId = alert.StudentId,
            CounselingDate = command.Request.CounselingDate,
            CounselingLocation = location,
            ProposedTargetScore = command.Request.ProposedTargetScore,
            MentorId = mentorId,
            Status = "SCHEDULED",
            Message = "Đã lên lịch hẹn tư vấn 3 bên định hướng lại mục tiêu thành công."
        };

        return Result<ScheduleCounselingResponseDto>.Success(response);
    }
}
