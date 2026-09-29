using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO phản hồi lộ trình học tập cá nhân hóa sau khi hoàn tất quy hoạch (Core Flow 2 - API 1)
/// </summary>
public record GenerateRoadmapResponseDto(
    Guid RoadmapId,
    Guid StudentId,
    Guid? DiagnosticSubmissionId,
    int TargetScore,
    int TotalMilestones,
    int CompletedMilestones,
    bool IsPruned,
    string? PrunedReason,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<RoadmapStageDto> Stages,
    IReadOnlyList<RoadmapNodeSummaryDto> Nodes
);
