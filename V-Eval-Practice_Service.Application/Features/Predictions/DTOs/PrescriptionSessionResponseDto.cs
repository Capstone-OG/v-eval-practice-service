using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

/// <summary>
/// DTO phản hồi đề ôn tập 3-5 câu biến thể cho dạng bài trong Toa thuốc bù điểm (Core Flow 6 - API 2)
/// </summary>
public class PrescriptionSessionResponseDto
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string PatternId { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public double TargetDeltaScore { get; set; }
    public int SuggestedDurationMinutes { get; set; } = 15;
    public int TotalQuestions { get; set; }
    public List<PrescriptionQuestionDto> Questions { get; set; } = new();
}

public class PrescriptionQuestionDto
{
    public Guid QuestionId { get; set; }
    public int QuestionOrder { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<PrescriptionOptionDto> Options { get; set; } = new();
    public double DifficultyB { get; set; } = 0.2;
}

public class PrescriptionOptionDto
{
    public string OptionId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
