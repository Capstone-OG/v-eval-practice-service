using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.JoinLiveSession;

public class JoinLiveSessionCommandHandler : IRequestHandler<JoinLiveSessionCommand, Result<JoinLiveSessionResponseDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<JoinLiveSessionCommandHandler> _logger;

    public JoinLiveSessionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<JoinLiveSessionCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<JoinLiveSessionResponseDto>> Handle(JoinLiveSessionCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Student {StudentId} joining Live Session {SessionId}", request.StudentId, request.SessionId);

        // 1. Kiểm tra sự tồn tại của buổi Live
        var session = await _liveSessionRepository.GetByIdAsync(request.SessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Live Session {SessionId} not found.", request.SessionId);
            return Result<JoinLiveSessionResponseDto>.Failure(
                Error.NotFound("LiveSessionNotFound", $"Không tìm thấy buổi học Live Q&A có ID {request.SessionId}."));
        }

        // 2. Kiểm tra trạng thái buổi học
        if (string.Equals(session.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Live Session {SessionId} is cancelled.", request.SessionId);
            return Result<JoinLiveSessionResponseDto>.Failure(
                Error.Validation("LiveSessionCancelled", "Buổi học này đã bị hủy bỏ."));
        }

        if (string.IsNullOrWhiteSpace(session.MeetingUrl))
        {
            _logger.LogWarning("Live Session {SessionId} has no meeting URL configured.", request.SessionId);
            return Result<JoinLiveSessionResponseDto>.Failure(
                Error.Validation("NoMeetingUrl", "Buổi học này chưa được cấu hình đường link phòng học trực tuyến."));
        }

        // 3. Ghi nhận thời gian tham gia vào lớp học (JoinedAt), bảo lưu quyền điểm danh chính thức cho Giảng viên (API 12)
        var attendance = await _liveSessionRepository.GetAttendanceAsync(request.SessionId, request.StudentId, ct);
        var now = DateTime.UtcNow;

        if (attendance == null)
        {
            attendance = new LiveSessionAttendance
            {
                AttendanceId = Guid.NewGuid(),
                SessionId = session.SessionId,
                StudentId = request.StudentId,
                AttendanceStatus = "NOT_ATTENDED", // Chờ giảng viên xác nhận điểm danh ở API 12
                JoinedAt = now
            };

            await _liveSessionRepository.AddAttendanceAsync(attendance, ct);
            _logger.LogInformation("Created new attendance record with JoinedAt={JoinedAt} for Student {StudentId} in Session {SessionId}", now, request.StudentId, request.SessionId);
        }
        else
        {
            // Chỉ cập nhật thời điểm vào lớp JoinedAt nếu chưa có, giữ nguyên trạng thái điểm danh do Giảng viên quản lý
            attendance.JoinedAt ??= now;
            _logger.LogInformation("Recorded JoinedAt for Student {StudentId} in Session {SessionId} (AttendanceStatus remains '{Status}')", request.StudentId, request.SessionId, attendance.AttendanceStatus);
        }

        await _liveSessionRepository.SaveChangesAsync(ct);

        var response = new JoinLiveSessionResponseDto
        {
            SessionId = session.SessionId,
            StudentId = request.StudentId,
            Title = session.Title,
            MeetingUrl = session.MeetingUrl,
            ScheduledAt = session.ScheduledAt,
            DurationMinutes = session.DurationMinutes,
            AttendanceStatus = attendance.AttendanceStatus,
            JoinedAt = attendance.JoinedAt ?? now,
            Message = "Lấy đường dẫn phòng học trực tuyến thành công. Đã ghi nhận thời gian vào lớp, trạng thái chuyên cần chính thức sẽ do giảng viên xác nhận tại buổi học."
        };

        return Result<JoinLiveSessionResponseDto>.Success(response);
    }
}
