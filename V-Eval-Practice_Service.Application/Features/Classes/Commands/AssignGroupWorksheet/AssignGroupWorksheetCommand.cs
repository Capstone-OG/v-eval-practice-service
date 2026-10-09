using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Classes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Classes.Commands.AssignGroupWorksheet;

public record AssignGroupWorksheetCommand(
    Guid ClassId,
    Guid GroupId,
    string WorksheetId,
    string WorksheetTitle
) : IRequest<Result<AssignWorksheetResponseDto>>;
