using System;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO yêu cầu ghi nhận thời gian xem video bài giảng lý thuyết (Core Flow 2 - API 4)
/// </summary>
public record TrackVideoRequestDto(
    int WatchedDurationSeconds,
    int TotalDurationSeconds
);
