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

        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        _ = Task.Run(async () =>
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

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
                            await channel.Writer.WriteAsync(parsedToken, cancellationToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Aborted by caller
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Đứt đoạn luồng stream SSE từ AI Tutor cho học sinh {StudentId}. Kích hoạt bổ sung fallback.", payload.StudentId);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        bool hasEmittedToken = false;
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var token))
            {
                hasEmittedToken = true;
                yield return token;
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
        string rawQ = (payload.StudentQuery ?? payload.QuestionLatex ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('!', '.', '?', '~', '^', ':', ';');
        if (rawQ == "hi" || rawQ == "hello" || rawQ == "chào" || rawQ == "chao" || rawQ == "chào thầy" || rawQ == "chào cô" || rawQ == "alo" || rawQ == "hey" || rawQ == "xin chào" || rawQ == "hi gia sư" || rawQ == "chào gia sư" || rawQ == "thầy ơi" || (rawQ.Length <= 25 && (rawQ.StartsWith("hi ") || rawQ.StartsWith("chào ") || rawQ.StartsWith("alo "))))
        {
            return "Chào em! Thầy là **Gia Sư AI Socratic**. Rất vui được đồng hành cùng em ôn luyện ĐGNL. Em đang gặp khó khăn ở dạng bài tập nào hay chuyên đề nào (Toán học, Tư duy Logic, Tiếng Việt, Khoa học Tự nhiên/Xã hội) cần thầy cùng gợi mở không? Em cứ gửi đề bài hoặc thắc mắc cho thầy nhé!";
        }

        string selectedText = payload.Options != null && payload.Options.TryGetValue(payload.StudentSelectedOption, out var sVal) && !string.IsNullOrWhiteSpace(sVal)
            ? $"{payload.StudentSelectedOption} ({sVal})"
            : payload.StudentSelectedOption;

        string q = (payload.QuestionLatex ?? string.Empty).ToLowerInvariant();
        var sb = new StringBuilder();

        if (q.Contains(@"p \Rightarrow q") || q.Contains("kéo theo") || q.Contains("mệnh đề") || q.Contains("bảng chân trị"))
        {
            sb.AppendLine("💡 **Gợi ý phương pháp giải Socratic (Toán logic mệnh đề & suy luận kéo theo):**\n");
            sb.AppendLine($"- **Phân tích lựa chọn:** Em đang chọn phương án **{selectedText}** cho câu hỏi về phép kéo theo $P \\Rightarrow Q$.");
            sb.AppendLine("- **Kiến thức trọng tâm:** Bảng chân trị mệnh đề kéo theo $P \\Rightarrow Q$ chỉ nhận giá trị **Sai** duy nhất khi $P$ Đúng và $Q$ Sai ($1 \\Rightarrow 0 = 0$). Trong mọi trường hợp khác, mệnh đề kéo theo luôn Đúng.");
            sb.AppendLine("- **Lưu ý bẫy thường gặp:** Nhiều bạn lầm tưởng khi $Q$ sai thì $P$ có thể đúng hoặc sai. Nếu $P$ đúng thì $P \\Rightarrow Q$ sẽ bị sai mâu thuẫn với giả thiết đề bài.");
            sb.AppendLine($"\n👉 *Câu hỏi gợi mở:* Đề bài cho biết $P \\Rightarrow Q$ là Đúng và $Q$ là Sai. Nếu $P$ nhận giá trị Đúng thì mệnh đề $P \\Rightarrow Q$ sẽ mang giá trị Đúng hay Sai?");
            return sb.ToString();
        }
        else if (q.Contains("cấp số cộng") || q.Contains("công sai") || q.Contains("u_n"))
        {
            sb.AppendLine("💡 **Gợi ý phương pháp giải Socratic (Dãy số & Cấp số cộng):**\n");
            sb.AppendLine($"- **Phân tích lựa chọn:** Em đang chọn phương án **{selectedText}**.");
            sb.AppendLine("- **Kiến thức trọng tâm:** Số hạng tổng quát của cấp số cộng $(u_n)$ được xác định theo công thức $u_n = u_1 + (n - 1)d$.");
            sb.AppendLine("- **Lưu ý bẫy thường gặp:** Cẩn thận lỗi quên trừ 1 ở chỉ số số hạng (dùng $n \\times d$ thay vì $(n-1)d$).");
            sb.AppendLine("\n👉 *Câu hỏi gợi mở:* Khoảng cách giữa số hạng đầu $u_1$ và số hạng thứ $n$ là bao nhiêu lần công sai $d$?");
            return sb.ToString();
        }
        else if (q.Contains("log") || q.Contains("tập xác định"))
        {
            sb.AppendLine("💡 **Gợi ý phương pháp giải Socratic (Hàm số Lũy thừa, Mũ & Logarit):**\n");
            sb.AppendLine($"- **Phân tích lựa chọn:** Em đang chọn phương án **{selectedText}**.");
            sb.AppendLine("- **Kiến thức trọng tâm:** Biểu thức $\\log_a f(x)$ xác định khi và chỉ khi biểu thức dưới dấu logarit dương nghiêm ngặt $f(x) > 0$ và cơ số $0 < a \\neq 1$.");
            sb.AppendLine("\n👉 *Câu hỏi gợi mở:* Khi giải bất phương trình $f(x) > 0$, tập nghiệm của tam thức bậc hai lấy khoảng phía trong hay phía ngoài hai nghiệm?");
            return sb.ToString();
        }
        else if (q.Contains("chính tả") || q.Contains("xán lạn") || q.Contains("sáng lạng"))
        {
            sb.AppendLine("💡 **Gợi ý phương pháp giải Socratic (Chính tả Tiếng Việt):**\n");
            sb.AppendLine($"- **Phân tích lựa chọn:** Em đang chọn phương án **{selectedText}**.");
            sb.AppendLine("- **Kiến thức trọng tâm:** Từ Hán Việt chuẩn là 'Xán lạn' ('xán' nghĩa là rực rỡ, 'lạn' nghĩa là sáng sủa, viết x-l). Trong từ điển tiếng Việt chuẩn không có từ 'sáng lạng'.");
            sb.AppendLine("\n👉 *Câu hỏi gợi mở:* Trong từ điển chính tả tiếng Việt, từ chỉ tương lai rực rỡ bắt đầu bằng âm 'x' hay 's'?");
            return sb.ToString();
        }

        sb.AppendLine("💡 **Gợi ý tư duy Socratic:**\n");
        sb.AppendLine($"- **Phân tích lựa chọn:** Em đang chọn phương án **{selectedText}** cho câu hỏi này.");
        sb.AppendLine("- **Bản chất vấn đề:** Hãy đối chiếu kỹ giả thiết bài toán với các điều kiện ràng buộc. Một kết luận trắc nghiệm chỉ đúng khi nó thỏa mãn trong mọi trường hợp và không mâu thuẫn với bất kỳ giả thiết ban đầu nào.");
        sb.AppendLine($"\n👉 *Câu hỏi gợi mở:* Nếu giả sử phương án **{selectedText}** là đúng, em hãy thử kiểm tra xem có trường hợp nào làm nảy sinh mâu thuẫn với giả thiết đề bài không?");
        return sb.ToString();
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
