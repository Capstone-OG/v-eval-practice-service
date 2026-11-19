using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoClusterThematicClasses;

public record AutoClusterThematicClassesCommand(
    Guid CampusId,
    int MaxK = 12,
    int MinCapacity = 5,
    int MaxCapacity = 20,
    bool DryRun = false,
    Guid? ManagerCampusId = null,
    double FoundationThreshold = 0.50,
    double AdvancedThreshold = 0.75,
    bool IncludeEmptyClasses = true,
    bool PreserveExistingEnrollments = true
) : IRequest<Result<AutoClusterResponseDto>>;
