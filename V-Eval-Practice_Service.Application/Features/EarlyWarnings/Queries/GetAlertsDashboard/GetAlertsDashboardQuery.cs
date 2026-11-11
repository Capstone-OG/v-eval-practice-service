using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetAlertsDashboard;

public record GetAlertsDashboardQuery(
    Guid? ClassId,
    Guid? MentorId,
    string? AlertType = null,
    bool? IsResolved = false) : IRequest<Result<EarlyWarningsDashboardResponseDto>>;
