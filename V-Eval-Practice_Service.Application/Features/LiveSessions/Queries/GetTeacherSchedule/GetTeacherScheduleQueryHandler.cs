using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetTeacherSchedule;

public class GetTeacherScheduleQueryHandler : IRequestHandler<GetTeacherScheduleQuery, Result<TeacherScheduleDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<GetTeacherScheduleQueryHandler> _logger;

    public GetTeacherScheduleQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<GetTeacherScheduleQueryHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<TeacherScheduleDto>> Handle(GetTeacherScheduleQuery request, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving live teaching schedule for TeacherId: {TeacherId}", request.TeacherId);

        var sessions = await _liveSessionRepository.GetSessionsForTeacherAsync(request.TeacherId, ct);

        var sessionDtos = new List<TeacherLiveSessionItemDto>();

        foreach (var s in sessions)
        {
            var enrolledCount = await _liveSessionRepository.GetEnrolledStudentCountByClassIdAsync(s.ClassId, ct);
            var attendedCount = s.Attendances.Count(a => string.Equals(a.AttendanceStatus, "ATTENDED", System.StringComparison.OrdinalIgnoreCase));
            var absentCount = s.Attendances.Count(a => string.Equals(a.AttendanceStatus, "ABSENT", System.StringComparison.OrdinalIgnoreCase));

            sessionDtos.Add(new TeacherLiveSessionItemDto
            {
                SessionId = s.SessionId,
                ClassId = s.ClassId,
                ClassName = s.Class?.Name ?? "Lớp học trực tuyến",
                Title = s.Title,
                Description = s.Description,
                ScheduledAt = s.ScheduledAt,
                DurationMinutes = s.DurationMinutes,
                MeetingUrl = s.MeetingUrl,
                RecordingUrl = s.RecordingUrl,
                IsRecorded = s.IsRecorded,
                Status = s.Status,
                TotalEnrolledStudents = enrolledCount,
                TotalAttended = attendedCount,
                TotalAbsent = absentCount
            });
        }

        var result = new TeacherScheduleDto
        {
            TeacherId = request.TeacherId,
            TotalSessions = sessionDtos.Count,
            Sessions = sessionDtos
        };

        return Result<TeacherScheduleDto>.Success(result);
    }
}
