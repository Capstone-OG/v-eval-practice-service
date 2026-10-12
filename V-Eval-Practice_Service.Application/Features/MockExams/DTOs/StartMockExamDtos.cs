using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record StartMockExamRequestDto(
    Guid StudentId
);

public record MockExamQuestionOptionDto(
    string OptionId,
    string Content
);

public record MockExamQuestionDto(
    Guid QuestionId,
    int QuestionOrder,
    string Content,
    IReadOnlyList<MockExamQuestionOptionDto> Options,
    int DifficultyLevel,
    Guid SkillId,
    string SkillName
);

public record StartMockExamResponseDto(
    Guid SubmissionId,
    Guid ExamId,
    Guid StudentId,
    string ExamTitle,
    int DurationMinutes,
    int TotalQuestions,
    DateTime StartedAt,
    string Status,
    IReadOnlyList<MockExamQuestionDto> Questions
);
