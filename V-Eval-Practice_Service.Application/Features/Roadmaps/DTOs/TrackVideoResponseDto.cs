using System;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO phản hồi kết quả ghi nhận tiến độ xem video bài giảng (Core Flow 2 - API 4)
/// </summary>
public record TrackVideoResponseDto(
    Guid NodeId,
    int WatchedDurationSeconds,
    int TotalDurationSeconds,
    double WatchPercentage,
    bool IsQuizEligible,
    string Status,
    string Message
);
