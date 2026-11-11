using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Stages.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Stages.Commands.SubmitRemedial;

/// <summary>
/// Command nộp bài gói cứu trợ phụ đạo (Remedial Node)
/// </summary>
public record SubmitRemedialCommand(
    Guid StageProgressId,
    SubmitRemedialRequestDto Request
) : IRequest<Result<SubmitRemedialResponseDto>>;
