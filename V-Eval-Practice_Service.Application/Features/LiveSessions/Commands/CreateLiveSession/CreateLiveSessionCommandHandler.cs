using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CreateLiveSession;

public class CreateLiveSessionCommandHandler : IRequestHandler<CreateLiveSessionCommand, Result<LiveSessionSummaryDto>>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ILogger<CreateLiveSessionCommandHandler> _logger;

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        ILogger<CreateLiveSessionCommandHandler> logger)
    {
        _liveSessionRepository = liveSessionRepository;
        _logger = logger;
    }

    public async Task<Result<LiveSessionSummaryDto>> Handle(
        CreateLiveSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra sự tồn tại của lớp học cơ sở
        var targetClass = await _liveSessionRepository.GetClassByIdAsync(request.ClassId, cancellationToken);
        if (targetClass == null)
        {
            return Result<LiveSessionSummaryDto>.Failure(
                Error.NotFound("Class.NotFound", $"Không tìm thấy lớp học cơ sở với ID {request.ClassId}."));
        }

        // 2. Xác định giáo viên phụ trách (ưu tiên TeacherId truyền vào, fallback về TeacherId của lớp)
        var teacherId = request.TeacherId ?? targetClass.TeacherId;

        // 3. Khởi tạo đường link phòng học trực tuyến chuẩn hóa nếu chưa cung cấp
        var meetingUrl = !string.IsNullOrWhiteSpace(request.MeetingUrl)
            ? request.MeetingUrl.Trim()
            : $"https://meet.veval.edu.vn/{Guid.NewGuid().ToString("N")[..8]}";

        var session = new LiveSession
        {
            SessionId = Guid.NewGuid(),
            ClassId = request.ClassId,
            TeacherId = teacherId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            ScheduledAt = request.ScheduledAt.ToUniversalTime(),
            DurationMinutes = request.DurationMinutes,
            MeetingUrl = meetingUrl,
            Status = "SCHEDULED",
            CreatedAt = DateTime.UtcNow
        };

        await _liveSessionRepository.AddAsync(session, cancellationToken);
        await _liveSessionRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Live Session {SessionId} created for Class {ClassId} ('{ClassName}') scheduled at {ScheduledAt}",
            session.SessionId, targetClass.ClassId, targetClass.Name, session.ScheduledAt);

        var responseDto = new LiveSessionSummaryDto
        {
            SessionId = session.SessionId,
            ClassId = session.ClassId,
            ClassName = targetClass.Name,
            TeacherId = session.TeacherId,
            Title = session.Title,
            Description = session.Description,
            ScheduledAt = session.ScheduledAt,
            DurationMinutes = session.DurationMinutes,
            MeetingUrl = session.MeetingUrl,
            RecordingUrl = session.RecordingUrl,
            IsRecorded = session.IsRecorded,
            Status = session.Status,
            CreatedAt = session.CreatedAt
        };

        return Result<LiveSessionSummaryDto>.Success(responseDto);
    }
}
