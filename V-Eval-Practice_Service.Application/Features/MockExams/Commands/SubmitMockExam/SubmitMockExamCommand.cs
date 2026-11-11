using System;
using System.Collections.Generic;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

namespace V_Eval_Practice_Service.Application.Features.MockExams.Commands.SubmitMockExam;

public record SubmitMockExamCommand(
    Guid SubmissionId,
    IReadOnlyList<MockExamAnswerSubmissionDto> Answers
) : IRequest<Result<SubmitMockExamResponseDto>>;
