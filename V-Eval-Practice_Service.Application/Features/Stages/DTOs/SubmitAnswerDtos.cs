using System;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public class SubmitAnswerRequestDto
{
    public Guid StudentId { get; set; }
    public Guid QuestionId { get; set; }
    public string SelectedOption { get; set; } = string.Empty;
    public int TimeSpentSeconds { get; set; }
    public string? PatternId { get; set; }
}

public record SubmitAnswerResponseDto(
    Guid StageProgressId,
    Guid QuestionId,
    bool IsCorrect,
    string CorrectOption,
    double PriorPlt,
    double PosteriorPlt,
    bool IsLuckyGuess,
    string CurrentStep,
    string Status,
    int ConsecutiveAdvancedCorrect,
    int ConsecutiveIncorrect,
    bool IsMasteryAchieved,
    bool IsRemedialTriggered,
    string FeedbackMessage,
    string NextAction
);
