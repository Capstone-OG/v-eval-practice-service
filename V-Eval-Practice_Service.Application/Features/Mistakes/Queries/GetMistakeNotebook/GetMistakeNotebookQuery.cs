using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Queries.GetMistakeNotebook;

public record GetMistakeNotebookQuery(
    Guid StudentId,
    Guid? SkillId = null,
    string? CognitiveErrorTag = null,
    bool? IsMastered = null,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<Result<MistakeNotebookListResponseDto>>;
