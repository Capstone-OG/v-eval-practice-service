using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.ScheduleCounseling;

public record ScheduleCounselingCommand(
    Guid AlertId,
    Guid EffectiveMentorId,
    ScheduleCounselingRequestDto Request) : IRequest<Result<ScheduleCounselingResponseDto>>;
