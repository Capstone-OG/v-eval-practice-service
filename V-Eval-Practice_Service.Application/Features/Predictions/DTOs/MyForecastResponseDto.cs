using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

/// <summary>
/// Dữ liệu phản hồi toàn diện cho API tra cứu dải điểm dự báo & toa thuốc cá nhân (Core Flow 6)
/// </summary>
public class MyForecastResponseDto
{
    public Guid StudentId { get; set; }
    public ForecastDetailDto Forecast { get; set; } = new();
    public BalanceSheetDto BalanceSheet { get; set; } = new();
    public List<PrescriptionItemDto> Prescriptions { get; set; } = new();
}

public class ForecastDetailDto
{
    public int? PredictedScore { get; set; }
    public ScoreRangeDto ScoreRange { get; set; } = new();
    public double? Sem { get; set; }
    public double? ConfidenceRate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string CalculationSource { get; set; } = "MOCK_EXAM";
}

public class ScoreRangeDto
{
    public int? Min { get; set; }
    public int? Max { get; set; }
}

public class BalanceSheetDto
{
    public int? TargetScore { get; set; }
    public int? DeficitScore { get; set; }
    public string Status { get; set; } = "ON_TRACK";
}
