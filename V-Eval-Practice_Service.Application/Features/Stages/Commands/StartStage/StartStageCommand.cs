using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.StartStage;

public record StartStageCommand(
    Guid RoadmapNodeId,
    Guid StudentId
) : IRequest<Result<StartStageResponseDto>>;
