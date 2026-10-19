using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.EarlyWarnings.DTOs;

namespace V_Eval_Practice_Service.Application.Features.EarlyWarnings.Queries.GetMyAlerts;

public record GetMyAlertsQuery(
    Guid StudentId,
    bool? UnreadOnly = false,
    string? AlertType = null) : IRequest<Result<MyAlertsResponseDto>>;
