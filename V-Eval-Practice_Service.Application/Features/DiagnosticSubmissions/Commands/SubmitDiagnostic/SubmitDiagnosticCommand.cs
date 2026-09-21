using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.DiagnosticSubmissions.Commands.SubmitDiagnostic;

public record SubmitDiagnosticCommand(
    Guid StudentId,
    Guid ExamId,
    DateTime? StartedAt,
    List<QuestionAnswerInputDto> Answers
) : IRequest<Result<SubmitDiagnosticResponseDto>>;
