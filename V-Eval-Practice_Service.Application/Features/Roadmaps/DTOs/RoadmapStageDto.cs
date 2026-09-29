using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// Đại diện cho một Chặng học phân theo Miền năng lực / Môn học trong Lộ trình (Group by Domain)
/// </summary>
public record RoadmapStageDto(
    Guid DomainId,
    string DomainName,
    int TotalNodes,
    int CompletedNodes,
    IReadOnlyList<RoadmapNodeSummaryDto> Nodes
);
