using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;

namespace V_Eval_Practice_Service.Infrastructure.HttpClients;

public class ContentHttpClient : IContentHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;
    private readonly ILogger<ContentHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ContentHttpClient(
        IConfiguration configuration,
        ILogger<ContentHttpClient> logger,
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _serviceUrl = (configuration["ContentSettings:ServiceUrl"] 
                      ?? configuration["GrpcSettings:ContentServiceUrl"] 
                      ?? "http://localhost:5250").TrimEnd('/');
        _logger = logger;
    }

    public async Task<CreateAdaptiveQuizResponse?> CreateAdaptiveQuizAsync(
        CreateAdaptiveQuizRequest request,
        CancellationToken ct = default)
    {
        string endpoint = $"{_serviceUrl}/api/content/exams/quiz";
        _logger.LogInformation("Calling Content Service at {Endpoint} to package adaptive quiz with {Count} skills",
            endpoint, request.SkillIds.Count);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateAdaptiveQuizResponse>(JsonOptions, ct);
                if (result != null)
                {
                    _logger.LogInformation("Content Service generated quiz successfully: ExamId={ExamId}, Title='{Title}'",
                        result.ExamId, result.Title);
                    return result;
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Content Service returned HTTP {StatusCode}: {ErrorBody}",
                    response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Content Service at {Endpoint}", endpoint);
        }

        return null;
    }
}
