using System;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record FlaggedMockExamDto(
    Guid SubmissionId,
    Guid ExamId,
    Guid StudentId,
    Guid? EnrolledClassId,
    int TotalScore,
    int TotalCorrect,
    int TotalQuestions,
    int TabDepartureCount,
    string IntegrityStatus,
    string MentorVerdict,
    string? MentorNotes,
    DateTime StartedAt,
    DateTime CompletedAt,
    int SnapshotsCount,
    string Status,
    string StudentName = "",
    DateTime? SubmittedAt = null
);

