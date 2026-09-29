using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignTeacher;

public record AssignTeacherCommand(
    Guid ClassId,
    Guid TeacherId,
    Guid? AssignedBy
) : IRequest<Result<AssignTeacherResponseDto>>;
