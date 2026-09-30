using System;

namespace V_Eval_Practice_Service.Application.Features.Roadmaps.DTOs;

/// <summary>
/// DTO tóm tắt chặng học (Milestone / RoadmapNode) trong lộ trình học tập
/// </summary>
public record RoadmapNodeSummaryDto(
    Guid NodeId,
    Guid SkillId,
    string SkillName,
    Guid DomainId,
    string DomainName,
    int StepOrder,
    Guid? MaterialId,
    Guid? QuizExamId,
    Guid? LiveSessionId,
    string Status,
    bool IsPruned,
    DateTime? UnlockedAt,
    DateTime? CompletedAt,
    string DomainCode = ""
);
