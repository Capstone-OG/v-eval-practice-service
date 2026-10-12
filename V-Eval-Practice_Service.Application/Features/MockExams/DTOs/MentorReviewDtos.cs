using System;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record MentorReviewRequestDto(
    string Verdict,
    string? Notes
);

public record MentorReviewResponseDto(
    Guid SubmissionId,
    Guid ExamId,
    Guid StudentId,
    int TotalScore,
    string Status,
    string IntegrityStatus,
    string MentorVerdict,
    string? MentorNotes,
    DateTime ReviewedAt,
    string Message
);
