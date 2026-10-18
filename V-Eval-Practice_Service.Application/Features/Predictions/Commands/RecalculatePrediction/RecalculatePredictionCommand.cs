using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Commands.RecalculatePrediction;

public record RecalculatePredictionCommand(
    Guid EffectiveStudentId,
    RecalculatePredictionRequestDto Request
) : IRequest<Result<RecalculatePredictionResponseDto>>;
