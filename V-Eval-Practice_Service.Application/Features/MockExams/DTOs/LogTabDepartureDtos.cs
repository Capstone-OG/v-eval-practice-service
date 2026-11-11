using System;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record LogTabDepartureRequestDto(
    string? Reason = null
);

public record LogTabDepartureResponseDto(
    Guid SubmissionId,
    int TabDepartureCount,
    string IntegrityStatus,
    bool IsFlagged,
    string WarningMessage,
    DateTime LoggedAt
);
