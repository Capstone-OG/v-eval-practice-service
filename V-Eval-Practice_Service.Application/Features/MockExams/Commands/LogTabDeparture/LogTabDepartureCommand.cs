using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.LogTabDeparture;

public record LogTabDepartureCommand(
    Guid SubmissionId,
    string? Reason = null
) : IRequest<Result<LogTabDepartureResponseDto>>;
