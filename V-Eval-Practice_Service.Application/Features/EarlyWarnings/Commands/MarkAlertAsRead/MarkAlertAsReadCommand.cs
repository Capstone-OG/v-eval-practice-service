using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Commands.MarkAlertAsRead;

public record MarkAlertAsReadCommand(
    Guid AlertId,
    Guid StudentId) : IRequest<Result<MarkAlertAsReadResponseDto>>;
