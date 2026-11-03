using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.API.Controllers.Base;
using V_Eval_Practice_Service.Application.Features.Payments.Commands.CreatePaymentLink;
using V_Eval_Practice_Service.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using V_Eval_Practice_Service.Application.Features.Payments.Queries.GetPaymentStatus;
using V_Eval_Practice_Service.Application.Features.Payments.Queries.GetPaymentTransactions;

namespace V_Eval_Practice_Service.API.Controllers;

[Route("api/practice/payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(ILogger<PaymentsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Khởi tạo đường link thanh toán PayOS (VietQR) cho học phí khóa học
    /// </summary>
    [HttpPost("create-link")]
    public async Task<IActionResult> CreatePaymentLink(
        [FromBody] CreatePaymentLinkApiRequest request,
        CancellationToken cancellationToken)
    {
        var parentId = request.ParentId;
        if (!parentId.HasValue || parentId == Guid.Empty)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var parsedUserId))
            {
                parentId = parsedUserId;
            }
            else
            {
                parentId = request.StudentId;
            }
        }

        var command = new CreatePaymentLinkCommand(
            request.StudentId,
            parentId.Value,
            request.CourseId,
            request.ReturnUrl,
            request.CancelUrl
        );

        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Webhook IPN nhận kết quả thanh toán từ cổng thanh toán trực tuyến PayOS
    /// </summary>
    [HttpPost("payos-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessPayOsWebhook(CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var rawPayload = await reader.ReadToEndAsync(cancellationToken);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            _logger.LogInformation("--- NHẬN WEBHOOK TỪ CỔNG THANH TOÁN PAYOS --- IP: {IpAddress}", ipAddress);

            var command = new ProcessPayOsWebhookCommand(rawPayload, ipAddress);
            var result = await Mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(new { success = true });
            }

            _logger.LogWarning("Webhook PayOS xử lý không thành công: {Error}", result.Error.Description);
            return Ok(new { success = false, message = result.Error.Description });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi ngoại lệ khi xử lý Webhook PayOS: {Message}", ex.Message);
            return Ok(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Tra cứu trạng thái giao dịch thanh toán theo orderCode
    /// </summary>
    [HttpGet("status/{orderCode:long}")]
    public async Task<IActionResult> GetStatus(long orderCode, CancellationToken cancellationToken)
    {
        var query = new GetPaymentStatusQuery(orderCode);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lấy danh sách lịch sử kiểm toán giao dịch (Audit Logs) của một thanh toán
    /// </summary>
    [HttpGet("transactions/{paymentId:guid}")]
    public async Task<IActionResult> GetTransactions(Guid paymentId, CancellationToken cancellationToken)
    {
        var query = new GetPaymentTransactionsQuery(paymentId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult(result);
    }
}

public class CreatePaymentLinkApiRequest
{
    public Guid StudentId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid CourseId { get; set; }
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

