using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record MockExamSnapshotDto(
    Guid Id,
    string SnapshotUrl,
    DateTime CapturedAt,
    bool FaceDetected
);

public record MockExamAnswerDetailDto(
    Guid QuestionId,
    string? SelectedOption,
    bool IsCorrect,
    int TimeSpentSeconds
);

public record GetMockExamDetailResponseDto(
    Guid SubmissionId,
    Guid ExamId,
    Guid StudentId,
    string ExamType,
    int TotalScore,
    int TotalCorrect,
    int TotalQuestions,
    int TotalTimeSpentSeconds,
    DateTime StartedAt,
    DateTime CompletedAt,
    string Status,
    int TabDepartureCount,
    string IntegrityStatus,
    string MentorVerdict,
    string? MentorNotes,
    IReadOnlyList<MockExamSnapshotDto> Snapshots,
    IReadOnlyList<MockExamAnswerDetailDto> Answers
);
