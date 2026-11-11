using System;

namespace V_Eval_Practice_Service.Application.Features.MockExams.DTOs;

public record UploadSnapshotRequestDto(
    string SnapshotUrl,
    bool FaceDetected = true
);

public record UploadSnapshotResponseDto(
    Guid SnapshotId,
    Guid SubmissionId,
    string SnapshotUrl,
    DateTime CapturedAt,
    bool FaceDetected,
    int TotalSnapshotsCount,
    string Message
);
