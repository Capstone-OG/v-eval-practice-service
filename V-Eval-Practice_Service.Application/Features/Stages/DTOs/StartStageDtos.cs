using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public record StartStageRequestDto(
    Guid StudentId
);

public record PreviewQuestionOptionDto(
    string OptionId,
    string Content
);

public record PreviewQuestionDto(
    Guid QuestionId,
    int QuestionOrder,
    string Content,
    IReadOnlyList<PreviewQuestionOptionDto> Options,
    int DifficultyLevel,
    Guid SkillId,
    string SkillName
);

public record StartStageResponseDto(
    Guid StageProgressId,
    Guid RoadmapNodeId,
    Guid SkillId,
    string CurrentStep,
    string Status,
    decimal VideoWatchPercentage,
    double BktMasteryPlt,
    int ConsecutiveAdvancedCorrect,
    int ConsecutiveIncorrect,
    IReadOnlyList<PreviewQuestionDto> PreviewQuestions
);
