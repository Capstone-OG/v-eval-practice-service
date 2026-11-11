using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ResolveAlert;

public record ResolveAlertCommand(
    Guid AlertId,
    Guid EffectiveMentorId,
    ResolveAlertRequestDto Request) : IRequest<Result<ResolveAlertResponseDto>>;
