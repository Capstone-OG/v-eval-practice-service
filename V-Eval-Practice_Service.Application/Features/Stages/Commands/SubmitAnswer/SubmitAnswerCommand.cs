using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitAnswer;

public record SubmitAnswerCommand(
    Guid StageProgressId,
    SubmitAnswerRequestDto Request
) : IRequest<Result<SubmitAnswerResponseDto>>;
