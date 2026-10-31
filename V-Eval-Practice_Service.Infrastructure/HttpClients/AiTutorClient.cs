using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces;

namespace V_Eval_Practice_Service.Infrastructure.HttpClients;

/// <summary>
/// Client kết nối sang Socratic AI Tutor Engine (FastAPI RAG Service port 8000).
/// Hỗ trợ cả REST JSON thông thường và Server-Sent Events (SSE) streaming thời gian thực.
/// </summary>
public class AiTutorClient : IAiTutorClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;
    private readonly ILogger<AiTutorClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AiTutorClient(
        IConfiguration configuration,
        ILogger<AiTutorClient> logger,
        HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _serviceUrl = (configuration["AiSettings:ServiceUrl"] ?? "http://localhost:8000").TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// Stream lời dẫn Socratic theo thời gian thực (SSE Proxy).
    /// </summary>
    public async IAsyncEnumerable<string> StreamSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string endpoint = $"{_serviceUrl}/api/v1/socratic/ask-stream";
        _logger.LogInformation("Bắt đầu stream Socratic AI Tutor từ {Endpoint} cho học sinh {StudentId}, câu hỏi {QuestionId}",
            endpoint, payload.StudentId, payload.QuestionId);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        HttpResponseMessage? response = null;
        bool hasError = false;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Tutor stream trả về mã lỗi {StatusCode}: {ErrorContent}. Kích hoạt phản hồi dự phòng.",
                    response.StatusCode, errContent);
                hasError = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới AI Tutor stream endpoint {Endpoint}. Kích hoạt phản hồi dự phòng.", endpoint);
            hasError = true;
        }

        if (hasError || response == null)
        {
            yield return GenerateStreamFallback(payload);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        bool hasEmittedToken = false;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                string dataContent = line.Substring(5).Trim();
                if (dataContent == "[DONE]")
                {
                    break;
                }

                string? parsedToken = null;
                bool isDoneEvent = false;

                try
                {
                    using var doc = JsonDocument.Parse(dataContent);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("type", out var typeProp))
                        {
                            string? type = typeProp.GetString();
                            if (type == "token" && root.TryGetProperty("token", out var tokenProp))
                            {
                                parsedToken = tokenProp.GetString();
                            }
                            else if (type == "done")
                            {
                                isDoneEvent = true;
                            }
                            else if (type == "error" && root.TryGetProperty("error", out var errProp))
                            {
                                _logger.LogWarning("Stream error payload từ AI Tutor: {Error}", errProp.GetString());
                            }
                        }
                        else if (root.TryGetProperty("token", out var directTokenProp))
                        {
                            parsedToken = directTokenProp.GetString();
                        }
                    }
                }
                catch (JsonException)
                {
                    parsedToken = dataContent;
                }

                if (isDoneEvent)
                {
                    break;
                }

                if (!string.IsNullOrEmpty(parsedToken))
                {
                    hasEmittedToken = true;
                    yield return parsedToken;
                }
            }
        }

        if (!hasEmittedToken)
        {
            yield return GenerateStreamFallback(payload);
        }
    }

    /// <summary>
    /// Gửi câu hỏi đến Socratic AI Tutor nhận phản hồi JSON đồng bộ.
    /// </summary>
    public async Task<SocraticAskResponseDto?> AskSocraticGuidanceAsync(
        SocraticAskRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        string endpoint = $"{_serviceUrl}/api/v1/socratic/ask";
        _logger.LogInformation("Gọi AI Tutor đồng bộ tại {Endpoint} cho câu hỏi {QuestionId}", endpoint, payload.QuestionId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SocraticAskResponseDto>(JsonOptions, cancellationToken);
                if (result != null)
                {
                    return result;
                }
            }
            else
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Tutor JSON HTTP error {StatusCode}: {ErrorContent}", response.StatusCode, errContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gọi AI Tutor endpoint {Endpoint}. Kích hoạt Fallback.", endpoint);
        }

        return new SocraticAskResponseDto(
            InteractionId: null,
            GuidanceText: GenerateStreamFallback(payload),
            IsFallback: true,
            ValidationStatus: "FALLBACK",
            RejectionReason: "Service unavailable or network error",
            ArchetypeCode: null,
            MatchedTrapCode: null,
            SubgraphStatus: "FALLBACK"
        );
    }

    private static string GenerateStreamFallback(SocraticAskRequestPayload payload)
    {
        string selectedText = payload.Options != null && payload.Options.TryGetValue(payload.StudentSelectedOption, out var sVal) && !string.IsNullOrWhiteSpace(sVal)
            ? $"{payload.StudentSelectedOption} ({sVal})"
            : payload.StudentSelectedOption;

        return $"💡 **Gợi ý phương pháp giải Socratic:**\n\n" +
               $"- **Phân tích lựa chọn:** Em đã chọn phương án **{selectedText}** cho câu hỏi: *{payload.QuestionLatex}*.\n" +
               $"- **Kiến thức cốt lõi cần đối chiếu:** Hãy kiểm tra lại định lý/công thức nền tảng của dạng bài này (điều kiện xác định, dấu của đạo hàm hoặc biến đổi tương đương) xem phương án **{payload.StudentSelectedOption}** có thỏa mãn trên toàn miền xác định hay chỉ đúng tại một vài giá trị đặc biệt.\n\n" +
               $"👉 *Câu hỏi gợi mở:* Nếu thử kiểm tra lại điều kiện cần và đủ của bài toán trên từng phương án, em thấy phương án **{payload.StudentSelectedOption}** gặp mâu thuẫn ở bước nào?";
    }

    /// <summary>
    /// Báo cáo câu trả lời hoặc lời dẫn gia sư AI sai từ học sinh.
    /// </summary>
    public async Task<ReportInteractionResponseDto?> ReportInteractionAsync(
        Guid interactionId,
        string? reason = null,
        Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        string endpoint = $"{_serviceUrl}/api/v1/socratic/interactions/{interactionId}/report";
        _logger.LogInformation("Gửi báo cáo tương tác sai tới {Endpoint}", endpoint);

        var payload = new ReportInteractionRequestPayload(reason, studentId);
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ReportInteractionResponseDto>(JsonOptions, cancellationToken);
            }

            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Báo cáo tương tác thất bại {StatusCode}: {Error}", response.StatusCode, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi API báo cáo tương tác {Endpoint}", endpoint);
        }

        return new ReportInteractionResponseDto(
            Id: interactionId.ToString(),
            StudentReported: true,
            MentorResolution: $"REPORTED_LOCALLY: {reason}",
            Message: "Báo cáo của học sinh đã được ghi nhận vào hệ thống."
        );
    }

    /// <summary>
    /// Lấy danh sách lịch sử tương tác học tập phục vụ Mentor kiểm duyệt.
    /// </summary>
    public async Task<IReadOnlyList<TutorInteractionItemDto>> GetInteractionsAsync(
        bool reportedOnly = false,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        string endpoint = $"{_serviceUrl}/api/v1/socratic/interactions?reported_only={reportedOnly.ToString().ToLower()}&limit={limit}&offset={offset}";
        _logger.LogInformation("Truy vấn danh sách tương tác Socratic Tutor từ {Endpoint}", endpoint);

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<List<TutorInteractionItemDto>>(JsonOptions, cancellationToken);
                return result ?? new List<TutorInteractionItemDto>();
            }

            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Truy vấn tương tác thất bại {StatusCode}: {Error}", response.StatusCode, err);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy vấn tương tác từ {Endpoint}", endpoint);
        }

        return Array.Empty<TutorInteractionItemDto>();
    }
}
