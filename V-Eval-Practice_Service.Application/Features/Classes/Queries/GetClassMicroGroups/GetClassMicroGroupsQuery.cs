using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Queries.GetClassMicroGroups;

public record GetClassMicroGroupsQuery(
    Guid ClassId
) : IRequest<Result<List<ClassMicroGroupDto>>>;
