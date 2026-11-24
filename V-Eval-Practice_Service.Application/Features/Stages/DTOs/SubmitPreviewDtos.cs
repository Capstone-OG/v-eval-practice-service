using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public record PreviewAnswerSubmissionDto(
    Guid QuestionId,
    string SelectedOption,
    int TimeSpentSeconds
);

public record SubmitPreviewRequestDto(
    IReadOnlyList<PreviewAnswerSubmissionDto> Answers
);

public record PreviewQuestionFeedbackDto(
    Guid QuestionId,
    string SelectedOption,
    string CorrectOption,
    bool IsCorrect,
    string Explanation
);

public record SubmitPreviewResponseDto(
    Guid StageProgressId,
    Guid RoadmapNodeId,
    string CurrentStep,
    string Status,
    int TotalQuestions,
    int TotalCorrect,
    string Message,
    string NextAction,
    Guid? MaterialId,
    string? VideoUrl,
    IReadOnlyList<PreviewQuestionFeedbackDto>? Feedbacks = null
);
