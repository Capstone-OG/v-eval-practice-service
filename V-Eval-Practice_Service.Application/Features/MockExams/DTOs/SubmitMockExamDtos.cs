using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record MockExamAnswerSubmissionDto(
    Guid QuestionId,
    string SelectedOption,
    int TimeSpentSeconds
);

public record SubmitMockExamRequestDto(
    IReadOnlyList<MockExamAnswerSubmissionDto> Answers
);

public record MockExamQuestionResultDto(
    Guid QuestionId,
    string SelectedOption,
    string CorrectOption,
    bool IsCorrect,
    int TimeSpentSeconds
);

public record SubmitMockExamResponseDto(
    Guid SubmissionId,
    Guid ExamId,
    Guid StudentId,
    int TotalQuestions,
    int TotalCorrect,
    int TotalScore,
    int TotalTimeSpentSeconds,
    string IntegrityStatus,
    string Status,
    int TabDepartureCount,
    int MistakesAddedCount,
    bool IsUnderReview,
    string Message,
    DateTime CompletedAt,
    IReadOnlyList<MockExamQuestionResultDto> AnswerBreakdown
);
