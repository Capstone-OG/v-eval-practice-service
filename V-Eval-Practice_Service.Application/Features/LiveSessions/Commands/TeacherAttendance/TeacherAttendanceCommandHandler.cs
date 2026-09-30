using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.TeacherAttendance;

public class TeacherAttendanceCommandHandler : IRequestHandler<TeacherAttendanceCommand, Result<TeacherAttendanceResponseDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<TeacherAttendanceCommandHandler> _logger;

    public TeacherAttendanceCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<TeacherAttendanceCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<TeacherAttendanceResponseDto>> Handle(TeacherAttendanceCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Processing teacher attendance for Session {SessionId} by {GradedBy}", request.SessionId, request.GradedBy);

        var session = await _liveSessionRepository.GetByIdAsync(request.SessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Live Session {SessionId} not found.", request.SessionId);
            return Result<TeacherAttendanceResponseDto>.Failure(
                Error.NotFound("LiveSessionNotFound", $"Không tìm thấy buổi học Live Q&A có ID {request.SessionId}."));
        }

        var now = DateTime.UtcNow;
        int attendedCount = 0;
        int absentCount = 0;

        foreach (var item in request.Items)
        {
            var normalizedStatus = item.AttendanceStatus.Trim().ToUpperInvariant();
            if (normalizedStatus == "ATTENDED")
            {
                attendedCount++;
            }
            else
            {
                absentCount++;
            }

            var attendance = await _liveSessionRepository.GetAttendanceAsync(session.SessionId, item.StudentId, ct);
            if (attendance == null)
            {
                // Học sinh chưa từng bấm Join trong hệ thống, giáo viên tạo bản ghi điểm danh
                attendance = new LiveSessionAttendance
                {
                    AttendanceId = Guid.NewGuid(),
                    SessionId = session.SessionId,
                    StudentId = item.StudentId,
                    AttendanceStatus = normalizedStatus,
                    JoinedAt = null // Học sinh chưa từng bấm vào phòng qua web
                };
                await _liveSessionRepository.AddAttendanceAsync(attendance, ct);
            }
            else
            {
                // Giáo viên chỉ cập nhật trạng thái chuyên cần (ATTENDED / ABSENT),
                // bảo lưu nguyên vẹn thời gian vào lớp JoinedAt do chính học sinh ghi nhận ở API 11
                attendance.AttendanceStatus = normalizedStatus;
            }
        }

        await _liveSessionRepository.SaveChangesAsync(ct);

        _logger.LogInformation("Completed attendance grading for Session {SessionId}. Attended: {Attended}, Absent: {Absent}",
            session.SessionId, attendedCount, absentCount);

        var response = new TeacherAttendanceResponseDto
        {
            SessionId = session.SessionId,
            TotalGraded = request.Items.Count,
            TotalAttended = attendedCount,
            TotalAbsent = absentCount,
            GradedAt = now,
            Message = $"Điểm danh thành công cho {request.Items.Count} học sinh ({attendedCount} tham gia, {absentCount} vắng mặt)."
        };

        return Result<TeacherAttendanceResponseDto>.Success(response);
    }
}
