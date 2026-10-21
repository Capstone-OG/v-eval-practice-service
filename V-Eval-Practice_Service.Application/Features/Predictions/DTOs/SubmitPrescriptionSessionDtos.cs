using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

public class SubmitPrescriptionSessionRequestDto
{
    public Guid SessionId { get; set; }
    public List<PrescriptionAnswerItemDto> Answers { get; set; } = new();
}

public class PrescriptionAnswerItemDto
{
    public Guid QuestionId { get; set; }
    public string SelectedOption { get; set; } = string.Empty;
    public int TimeSpentSeconds { get; set; }
}

public class SubmitPrescriptionSessionResponseDto
{
    public string PatternId { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public double Accuracy { get; set; }
    public double PreviousMasteryPlt { get; set; }
    public double NewMasteryPlt { get; set; }
    public bool IsMastered { get; set; }
    public double PointsRecovered { get; set; }
    public int RemainingDeficit { get; set; }
    public string Message { get; set; } = string.Empty;
}
