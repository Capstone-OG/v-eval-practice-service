using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Queries.GetMyLiveSchedule;

public class GetMyLiveScheduleQueryHandler : IRequestHandler<GetMyLiveScheduleQuery, Result<MyLiveScheduleDto>>
{
    private readonly IClassEnrollmentRepository _enrollmentRepository;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<GetMyLiveScheduleQueryHandler> _logger;

    public GetMyLiveScheduleQueryHandler(
        IClassEnrollmentRepository enrollmentRepository,
        ILiveSessionRepository liveSessionRepository,
        ILogger<GetMyLiveScheduleQueryHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<MyLiveScheduleDto>> Handle(GetMyLiveScheduleQuery request, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving live schedule for StudentId: {StudentId}", request.StudentId);

        var enrollment = await _enrollmentRepository.GetEnrollmentByStudentIdAsync(request.StudentId, ct);
        if (enrollment == null)
        {
            return Result<MyLiveScheduleDto>.Success(new MyLiveScheduleDto
            {
                StudentId = request.StudentId,
                EnrolledClassId = null,
                ClassName = "Chưa ghi danh vào lớp học",
                TotalSessions = 0,
                Sessions = new List<LiveSessionScheduleItemDto>()
            });
        }

        var sessions = await _liveSessionRepository.GetUpcomingSessionsForStudentAsync(request.StudentId, ct);

        var sessionDtos = sessions.Select(s =>
        {
            var attendance = s.Attendances.FirstOrDefault(a => a.StudentId == request.StudentId);
            return new LiveSessionScheduleItemDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                Description = s.Description,
                ScheduledAt = s.ScheduledAt,
                DurationMinutes = s.DurationMinutes,
                MeetingUrl = s.MeetingUrl,
                RecordingUrl = s.RecordingUrl,
                IsRecorded = s.IsRecorded,
                Status = s.Status,
                MyAttendanceStatus = attendance?.AttendanceStatus ?? "NOT_ATTENDED",
                IsMakeupQuizPassed = attendance?.IsMakeupQuizPassed,
                ClassId = s.ClassId,
                ClassName = s.Class?.Name ?? string.Empty,
                DomainCode = s.Class?.DomainCode
            };
        }).ToList();

        var result = new MyLiveScheduleDto
        {
            StudentId = request.StudentId,
            EnrolledClassId = enrollment.ClassId,
            ClassName = enrollment.Class?.Name ?? "Lớp học trực tuyến",
            TotalSessions = sessionDtos.Count,
            Sessions = sessionDtos
        };

        return Result<MyLiveScheduleDto>.Success(result);
    }
}
