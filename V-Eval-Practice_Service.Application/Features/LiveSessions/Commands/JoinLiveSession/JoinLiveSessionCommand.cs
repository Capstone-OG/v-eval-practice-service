using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.LiveSessions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.LiveSessions.Commands.JoinLiveSession;

public record JoinLiveSessionCommand(
    Guid SessionId,
    Guid StudentId
) : IRequest<Result<JoinLiveSessionResponseDto>>;
