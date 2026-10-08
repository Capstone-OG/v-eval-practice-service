using System;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public record TrackVideoRequestDto(
    Guid StudentId,
    int WatchedSeconds,
    int TotalSeconds
);

public record TrackVideoResponseDto(
    Guid StageProgressId,
    string CurrentStep,
    decimal VideoWatchPercentage,
    bool IsCompletedLearn,
    string NextAction,
    string Message
);
