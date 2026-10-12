using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public record NextQuestionOptionDto(
    string OptionId,
    string Content
);

public record NextQuestionResponseDto(
    Guid StageProgressId,
    string CurrentStep,
    string Status,
    double CurrentMasteryPlt,
    int AttemptOrder,
    bool IsFinished,
    string? Message,
    Guid? QuestionId = null,
    string? Content = null,
    IReadOnlyList<NextQuestionOptionDto>? Options = null,
    int? DifficultyLevel = null,
    double? ItemDifficultyB = null,
    double? ItemDiscriminationA = null,
    Guid? SkillId = null,
    string? SkillName = null
);
