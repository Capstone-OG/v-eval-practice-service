using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.SubmitDailyReview;

public record SubmitDailyReviewCommand(
    Guid MistakeNotebookId,
    SubmitDailyReviewRequestDto Request
) : IRequest<Result<SubmitDailyReviewResponseDto>>;
