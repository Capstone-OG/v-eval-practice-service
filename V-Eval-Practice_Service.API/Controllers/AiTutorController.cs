using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Models;

namespace V_Eval_Practice_Service.API.Controllers;

/// <summary>
/// Quản lý tương tác gia sư Socratic AI Tutor theo thời gian thực (Core Flow 4 - GraphRAG Tutor).
/// Hỗ trợ cả giao thức SSE Streaming (text/event-stream) và JSON đồng bộ.
/// </summary>
[ApiController]
[Route("api/practice/ai-tutor")]
[Route("api/v1/ai-tutor")]
[EnableRateLimiting("StrictTutorPolicy")]
public class AiTutorController : ApiControllerBase
{
    private readonly IAiTutorClient _aiTutorClient;
    private readonly ILogger<AiTutorController> _logger;

    public AiTutorController(
        IAiTutorClient aiTutorClient,
        ILogger<AiTutorController> logger)
    {
        _aiTutorClient = aiTutorClient;
        _logger = logger;
    }

    /// <summary>
    /// Stream lời dẫn Socratic theo thời gian thực qua Server-Sent Events (SSE).
    /// </summary>
    /// <remarks>
    /// - Yêu cầu xác thực học sinh qua Gateway Header `X-User-Id` hoặc trường `studentId` trong body.
    /// - Tích hợp Rate Limiting: Tối đa 10 requests / phút mỗi người dùng.
    /// - Kết quả được phát qua SSE với chuỗi `data: {"token": "..."}\n\n` và kết thúc bằng `data: [DONE]\n\n`.
    /// </remarks>
    [HttpPost("ask-stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task GetSocraticStreamAsync(
        [FromBody] AskTutorRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId(request.StudentId);
        if (studentId == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new
            {
                error = "Không tìm thấy định danh học sinh (X-User-Id header hoặc studentId payload)."
            }, cancellationToken);
            return;
        }

        if (request.QuestionId == Guid.Empty || string.IsNullOrWhiteSpace(request.QuestionLatex))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new
            {
                error = "QuestionId và QuestionLatex không được để trống."
            }, cancellationToken);
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
        Response.Headers.Append("Cache-Control", "no-cache, no-transform");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, leaveOpen: true);

        var payload = new SocraticAskRequestPayload(
            StudentId: studentId,
            QuestionId: request.QuestionId,
            QuestionLatex: request.QuestionLatex,
            Options: request.Options ?? new Dictionary<string, string>(),
            StudentSelectedOption: request.StudentWrongOption?.Trim().ToUpper() ?? "A",
            CorrectOption: request.CorrectOption?.Trim().ToUpper() ?? "A",
            SkillId: request.SkillId
        );

        try
        {
            await foreach (var token in _aiTutorClient.StreamSocraticGuidanceAsync(payload, cancellationToken))
            {
                var eventPayload = JsonSerializer.Serialize(new { token });
                await writer.WriteAsync($"data: {eventPayload}\n\n");
                await writer.FlushAsync(cancellationToken);
            }

            await writer.WriteAsync("data: [DONE]\n\n");
            await writer.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client đã hủy kết nối stream SSE Socratic Tutor (StudentId: {StudentId})", studentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý stream SSE Socratic Tutor cho StudentId: {StudentId}", studentId);
            try
            {
                var errPayload = JsonSerializer.Serialize(new { error = ex.Message });
                await writer.WriteAsync($"data: {errPayload}\n\n");
                await writer.FlushAsync(cancellationToken);
            }
            catch
            {
                // Ignore secondary write failures when client is closed
            }
        }
    }

    /// <summary>
    /// Nhận lời dẫn Socratic dưới dạng JSON đồng bộ (dành cho kiểm thử hoặc client không hỗ trợ SSE).
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(SocraticAskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AskSocraticJsonAsync(
        [FromBody] AskTutorRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId(request.StudentId);
        if (studentId == Guid.Empty)
        {
            return HandleResult(Result<SocraticAskResponseDto>.Failure(
                Error.Unauthorized("Auth.StudentIdRequired", "Không tìm thấy định danh học sinh (X-User-Id header hoặc studentId payload).")));
        }

        if (request.QuestionId == Guid.Empty || string.IsNullOrWhiteSpace(request.QuestionLatex))
        {
            return BadRequest(new { error = "QuestionId và QuestionLatex không được để trống." });
        }

        var payload = new SocraticAskRequestPayload(
            StudentId: studentId,
            QuestionId: request.QuestionId,
            QuestionLatex: request.QuestionLatex,
            Options: request.Options ?? new Dictionary<string, string>(),
            StudentSelectedOption: request.StudentWrongOption?.Trim().ToUpper() ?? "A",
            CorrectOption: request.CorrectOption?.Trim().ToUpper() ?? "A",
            SkillId: request.SkillId
        );

        var result = await _aiTutorClient.AskSocraticGuidanceAsync(payload, cancellationToken);
        if (result == null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Không nhận được phản hồi từ AI Tutor Engine." });
        }

        return Ok(result);
    }

    private Guid ResolveStudentId(Guid? requestStudentId)
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && Guid.TryParse(userIdHeader, out var parsedId))
        {
            return parsedId;
        }

        if (requestStudentId.HasValue && requestStudentId.Value != Guid.Empty)
        {
            return requestStudentId.Value;
        }

        return Guid.Empty;
    }
}

/// <summary>
/// Yêu cầu hỏi gia sư Socrates cho câu hỏi bài tập vừa làm sai.
/// </summary>
public record AskTutorRequest(
    Guid? StudentId,
    Guid QuestionId,
    string QuestionLatex,
    Dictionary<string, string>? Options,
    string StudentWrongOption,
    string CorrectOption,
    Guid? SkillId
);
