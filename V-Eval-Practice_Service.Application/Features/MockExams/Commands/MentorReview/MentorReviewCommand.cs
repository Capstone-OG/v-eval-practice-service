using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.MentorReview;

public record MentorReviewCommand(
    Guid SubmissionId,
    string Verdict,
    string? Notes
) : IRequest<Result<MentorReviewResponseDto>>;
