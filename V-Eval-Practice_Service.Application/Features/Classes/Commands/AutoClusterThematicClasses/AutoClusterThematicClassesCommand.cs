using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;

public record AutoClusterThematicClassesCommand(
    Guid CampusId,
    int MaxK = 8
) : IRequest<Result<AutoClusterResponseDto>>;
