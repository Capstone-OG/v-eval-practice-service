using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;

namespace V_Eval_Practice_Service.Infrastructure.HttpClients;

public class AiDiagnosticClient : IAiDiagnosticClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;
    private readonly ILogger<AiDiagnosticClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiDiagnosticClient(
        IConfiguration configuration,
        ILogger<AiDiagnosticClient> logger,
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _serviceUrl = (configuration["AiSettings:ServiceUrl"] ?? "http://localhost:8000").TrimEnd('/');
        _logger = logger;
    }

    public async Task<DiagnosticAnalyzeResponsePayload?> AnalyzeDiagnosticAsync(
        DiagnosticAnalyzeRequestPayload payload,
        CancellationToken ct = default)
    {
        string endpoint = $"{_serviceUrl}/api/v1/diagnostic/analyze";
        _logger.LogInformation("Calling AI Diagnostic Engine at {Endpoint} for student {StudentId}",
            endpoint, payload.StudentId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DiagnosticAnalyzeResponsePayload>(JsonOptions, ct);
                if (result != null)
                {
                    _logger.LogInformation("AI Diagnostic succeeded: theta_0={Theta0}, placement={Placement}",
                        result.Theta0, result.PlacementClass);
                    return result;
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("AI Diagnostic HTTP error {StatusCode}: {ErrorBody}. Activating local fallback.",
                    response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reach AI Diagnostic service at {Endpoint}. Activating resilient fallback.",
                endpoint);
        }

        // Resilient Fallback Calculation: Ensure student test completion is NEVER blocked
        return GenerateResilientFallback(payload);
    }

    private DiagnosticAnalyzeResponsePayload GenerateResilientFallback(DiagnosticAnalyzeRequestPayload payload)
    {
        int total = payload.Answers.Count;
        int correct = payload.Answers.Count(a => a.IsCorrect);
        double accuracy = total > 0 ? (double)correct / total : 0.5;

        // Approximate IRT theta: accuracy 50% -> 0.0, 100% -> +2.5, 0% -> -2.5
        double theta0 = Math.Max(-3.0, Math.Min(3.0, Math.Round((accuracy - 0.5) * 5.0, 4)));

        string placementClass = theta0 < -0.5
            ? "FOUNDATION"
            : theta0 > 0.5
                ? "BREAKTHROUGH"
                : "ACCELERATION";

        // Group by domain
        var domainGroups = payload.Answers
            .GroupBy(a => string.IsNullOrEmpty(a.DomainId) ? "general" : a.DomainId)
            .ToList();

        var domainScores = new List<DiagnosticDomainScoreResult>();
        var radarAxes = new List<DiagnosticRadarAxisResult>();
        double benchmarkPct = Math.Round((payload.TargetScore / 1200.0) * 100, 2);

        var domainNameMap = payload.DomainNames.ToDictionary(d => d.DomainId, d => d.DomainName);

        foreach (var group in domainGroups)
        {
            int dTotal = group.Count();
            int dCorrect = group.Count(x => x.IsCorrect);
            double dAcc = dTotal > 0 ? Math.Round((dCorrect / (double)dTotal) * 100, 2) : 0.0;
            double dTheta = Math.Max(-3.0, Math.Min(3.0, Math.Round(((dAcc / 100.0) - 0.5) * 5.0, 4)));

            string dName = domainNameMap.TryGetValue(group.Key, out var name)
                ? name
                : (group.Key == "general" ? "Năng lực tổng hợp" : group.Key);

            domainScores.Add(new DiagnosticDomainScoreResult(
                DomainId: group.Key,
                DomainName: dName,
                ThetaDomain: dTheta,
                TotalQuestions: dTotal,
                CorrectCount: dCorrect,
                AccuracyPct: dAcc
            ));

            radarAxes.Add(new DiagnosticRadarAxisResult(
                DomainId: group.Key,
                DomainName: dName,
                StudentPct: dAcc,
                BenchmarkPct: benchmarkPct
            ));
        }

        // BKT Initial Mastery Priors: P(L0) = Sigmoid(theta_skill), clamped [0.05, 0.95]
        var skillPriors = new List<DiagnosticSkillPriorResult>();
        var skillGroups = payload.Answers.GroupBy(a => a.SkillId);

        foreach (var sGroup in skillGroups)
        {
            int sTotal = sGroup.Count();
            int sCorrect = sGroup.Count(x => x.IsCorrect);
            double sAcc = sTotal > 0 ? (double)sCorrect / sTotal : 0.5;
            double sTheta = (sAcc - 0.5) * 5.0;

            // Sigmoid: 1 / (1 + exp(-theta))
            double pl0 = 1.0 / (1.0 + Math.Exp(-sTheta));
            pl0 = Math.Max(0.05, Math.Min(0.95, Math.Round(pl0, 4)));

            string domainId = sGroup.First().DomainId;

            skillPriors.Add(new DiagnosticSkillPriorResult(
                SkillId: sGroup.Key,
                DomainId: domainId,
                ThetaSkill: Math.Round(sTheta, 4),
                PL0: pl0,
                Source: "measured"
            ));
        }

        string placementName = placementClass switch
        {
            "FOUNDATION" => "Lớp Nền tảng (Foundation)",
            "BREAKTHROUGH" => "Lớp Bứt phá (Breakthrough)",
            _ => "Lớp Tăng tốc (Acceleration)"
        };

        string commentary = $"Dựa trên kết quả bài kiểm tra chẩn đoán, năng lực của bạn được xếp vào {placementName} (ước lượng θ = {theta0:F2}). Bạn đạt độ chính xác {accuracy * 100:F1}% trên 30 câu hỏi.";

        return new DiagnosticAnalyzeResponsePayload(
            StudentId: payload.StudentId,
            SubmissionId: payload.SubmissionId,
            Theta0: theta0,
            PlacementClass: placementClass,
            DomainScores: domainScores,
            SkillPriors: skillPriors,
            RadarChart: radarAxes,
            AiCommentary: commentary
        );
    }
}
