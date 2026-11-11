using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetPrescriptionSession;

public record GetPrescriptionSessionQuery(Guid StudentId, string PatternId) : IRequest<Result<PrescriptionSessionResponseDto>>;
