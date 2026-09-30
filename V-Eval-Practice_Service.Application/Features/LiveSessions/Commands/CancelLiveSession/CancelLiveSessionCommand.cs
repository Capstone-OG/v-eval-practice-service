using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.CancelLiveSession;

public record CancelLiveSessionCommand(
    Guid SessionId,
    string Reason,
    Guid? TeacherId = null
) : IRequest<Result<CancelLiveSessionResponseDto>>;
