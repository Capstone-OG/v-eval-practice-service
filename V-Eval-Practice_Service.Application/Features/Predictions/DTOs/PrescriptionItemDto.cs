using System;
using System.Text.Json.Serialization;

namespace V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

/// <summary>
/// Dạng bài tối ưu trong Toa thuốc bù điểm Knapsack ROI (Core Flow 6)
/// </summary>
public class PrescriptionItemDto
{
    [JsonPropertyName("pattern_id")]
    public string PatternId { get; set; } = string.Empty;

    [JsonPropertyName("pattern_name")]
    public string PatternName { get; set; } = string.Empty;

    [JsonPropertyName("skill_id")]
    public Guid SkillId { get; set; }

    [JsonPropertyName("delta_score")]
    public double DeltaScore { get; set; }

    [JsonPropertyName("study_hours_needed")]
    public double StudyHoursNeeded { get; set; }

    [JsonPropertyName("roi_score")]
    public double RoiScore { get; set; }

    [JsonPropertyName("priority_rank")]
    public int PriorityRank { get; set; }

    [JsonPropertyName("is_remedied")]
    public bool IsRemedied { get; set; } = false;
}
