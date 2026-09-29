using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO thể hiện dòng thời gian lộ trình học tập cá nhân hóa của học sinh (Core Flow 2 - API 2)
/// </summary>
public record RoadmapTimelineDto(
    Guid RoadmapId,
    Guid StudentId,
    Guid? DiagnosticSubmissionId,
    int TargetScore,
    int TotalMilestones,
    int CompletedMilestones,
    double ProgressPercentage,
    bool IsPruned,
    string? PrunedReason,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<RoadmapStageDto> Stages,
    IReadOnlyList<RoadmapNodeSummaryDto> Nodes
);
