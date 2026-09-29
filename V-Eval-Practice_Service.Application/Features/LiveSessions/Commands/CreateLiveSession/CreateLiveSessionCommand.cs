using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CreateLiveSession;

public record CreateLiveSessionCommand(
    Guid ClassId,
    Guid? TeacherId,
    string Title,
    string? Description,
    DateTime ScheduledAt,
    int DurationMinutes,
    string? MeetingUrl,
    Guid? CreatedBy
) : IRequest<Result<LiveSessionSummaryDto>>;
