using System;

namespace V_Eval_Practice_Service.Application.Features.Stages.DTOs;

public class ReflectCompleteRequestDto
{
    public Guid StudentId { get; set; }
    public int ConfidenceRating { get; set; }
    public string? LearnedSummary { get; set; }
    public string? MistakeNotes { get; set; }
}

public record ReflectCompleteResponseDto(
    Guid StageProgressId,
    Guid RoadmapNodeId,
    string Status,
    int ConfidenceRating,
    double FinalMasteryPlt,
    Guid? NextUnlockedNodeId,
    string Message
);
