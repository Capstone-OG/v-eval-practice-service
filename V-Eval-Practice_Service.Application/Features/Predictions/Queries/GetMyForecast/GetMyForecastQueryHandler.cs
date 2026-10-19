using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Predictions.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Predictions.Queries.GetMyForecast;

public class GetMyForecastQueryHandler : IRequestHandler<GetMyForecastQuery, Result<MyForecastResponseDto>>
{
    private readonly IScorePredictionRepository _predictionRepository;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, 
        PropertyNameCaseInsensitive = true 
    };

    public GetMyForecastQueryHandler(IScorePredictionRepository predictionRepository)
    {
        _predictionRepository = predictionRepository;
    }

    public async Task<Result<MyForecastResponseDto>> Handle(GetMyForecastQuery request, CancellationToken cancellationToken)
    {
        if (request.StudentId == Guid.Empty)
        {
            return Result<MyForecastResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh hợp lệ."));
        }

        var prediction = await _predictionRepository.GetLatestByStudentIdAsync(request.StudentId, cancellationToken);
        if (prediction == null)
        {
            return Result<MyForecastResponseDto>.Failure(
                Error.NotFound("Prediction.NotFound", "Chưa tìm thấy dữ liệu dự báo điểm cho học sinh này. Vui lòng hoàn thành bài thi thử chuẩn hóa hoặc kích hoạt tính toán."));
        }

        List<PrescriptionItemDto> prescriptions = new();
        if (!string.IsNullOrWhiteSpace(prediction.PrescriptionItems))
        {
            try
            {
                prescriptions = JsonSerializer.Deserialize<List<PrescriptionItemDto>>(prediction.PrescriptionItems, JsonOptions) ?? new();
            }
            catch
            {
                prescriptions = new List<PrescriptionItemDto>();
            }
        }

        var deficit = prediction.DeficitScore ?? 0;
        string balanceStatus = deficit switch
        {
            <= 0 => "ON_TRACK",
            <= 30 => "AT_RISK",
            _ => "CRITICAL"
        };

        var response = new MyForecastResponseDto
        {
            StudentId = prediction.StudentId,
            Forecast = new ForecastDetailDto
            {
                PredictedScore = prediction.PredictedScore,
                ScoreRange = new ScoreRangeDto
                {
                    Min = prediction.PredictedScoreMin,
                    Max = prediction.PredictedScoreMax
                },
                Sem = prediction.SemScore,
                ConfidenceRate = prediction.ConfidenceRate,
                LastUpdated = prediction.CreatedAt,
                CalculationSource = prediction.CalculationSource
            },
            BalanceSheet = new BalanceSheetDto
            {
                TargetScore = prediction.TargetScore,
                DeficitScore = prediction.DeficitScore,
                Status = balanceStatus
            },
            Prescriptions = prescriptions
        };

        return Result<MyForecastResponseDto>.Success(response);
    }
}
