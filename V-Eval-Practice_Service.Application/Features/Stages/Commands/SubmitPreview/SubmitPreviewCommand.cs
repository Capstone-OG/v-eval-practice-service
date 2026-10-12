using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitPreview;

public record SubmitPreviewCommand(
    Guid StageProgressId,
    IReadOnlyList<PreviewAnswerSubmissionDto> Answers
) : IRequest<Result<SubmitPreviewResponseDto>>;
