using System;
using System.Text.Json.Serialization;

namespace V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

public class RecalculatePredictionRequestDto
{
    [JsonPropertyName("student_id")]
    public Guid? StudentId { get; set; }

    [JsonPropertyName("trigger_source")]
    public string TriggerSource { get; set; } = "MOCK_EXAM"; // MOCK_EXAM, STAGE_PROGRESS, MANUAL

    [JsonPropertyName("reference_submission_id")]
    public Guid? ReferenceSubmissionId { get; set; }
}

public class RecalculatePredictionResponseDto
{
    public Guid PredictionId { get; set; }
    public Guid StudentId { get; set; }
    public int PredictedScore { get; set; }
    public ScoreRangeDto ScoreRange { get; set; } = new();
    public double SemScore { get; set; }
    public double ConfidenceRate { get; set; }
    public int TargetScore { get; set; }
    public int DeficitScore { get; set; }
    public string Status { get; set; } = "ON_TRACK";
    public string CalculationSource { get; set; } = "MOCK_EXAM";
    public int PrescriptionItemsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
