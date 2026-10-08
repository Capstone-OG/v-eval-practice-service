using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Queries.GetNextQuestion;

public record GetNextQuestionQuery(
    Guid StageProgressId
) : IRequest<Result<NextQuestionResponseDto>>;
