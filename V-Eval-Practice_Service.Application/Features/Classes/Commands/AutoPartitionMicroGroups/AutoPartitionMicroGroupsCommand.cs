using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AutoPartitionMicroGroups;

public record AutoPartitionMicroGroupsCommand(
    Guid ClassId,
    int? PreferredGroupSize = 4
) : IRequest<Result<AutoPartitionMicroGroupsResponseDto>>;
