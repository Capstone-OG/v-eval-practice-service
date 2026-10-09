using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetDailyReview;

public record GetDailyReviewQuery(
    Guid StudentId,
    DateTime? TargetDate = null
) : IRequest<Result<DailyReviewListResponseDto>>;
