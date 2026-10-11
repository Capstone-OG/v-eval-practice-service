using System;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Mistakes.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Mistakes.Commands.TagCognitiveError;

public record TagCognitiveErrorCommand(
    Guid MistakeNotebookId,
    TagCognitiveErrorRequestDto Request
) : IRequest<Result<TagCognitiveErrorResponseDto>>;
