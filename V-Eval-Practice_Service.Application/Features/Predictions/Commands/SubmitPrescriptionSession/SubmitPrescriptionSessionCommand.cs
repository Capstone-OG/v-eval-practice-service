using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Commands.SubmitPrescriptionSession;

public record SubmitPrescriptionSessionCommand(
    Guid StudentId,
    string PatternId,
    SubmitPrescriptionSessionRequestDto Request
) : IRequest<Result<SubmitPrescriptionSessionResponseDto>>;
