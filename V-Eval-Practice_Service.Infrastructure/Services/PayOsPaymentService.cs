using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;

namespace V_Eval_Practice_Service.Infrastructure.Services;

public class PayOsPaymentService : IPayOsPaymentService
{
    private readonly PayOSClient _payOs;
    private readonly ILogger<PayOsPaymentService> _logger;

    public PayOsPaymentService(IConfiguration configuration, ILogger<PayOsPaymentService> logger)
    {
        _logger = logger;

        var clientId = configuration["PayOS:ClientId"] ?? "mock-client-id";
        var apiKey = configuration["PayOS:ApiKey"] ?? "mock-api-key";
        var checksumKey = configuration["PayOS:ChecksumKey"] ?? "mock-checksum-key";

        _payOs = new PayOSClient(new PayOSOptions
        {
            ClientId = clientId,
            ApiKey = apiKey,
            ChecksumKey = checksumKey,
            HttpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            })
        });
    }

    public async Task<PaymentLinkResultDto> CreatePaymentLinkAsync(
        long orderCode,
        decimal amount,
        string description,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)amount,
            Description = description,
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl
        };

        var response = await _payOs.PaymentRequests.CreateAsync(paymentRequest);

        return new PaymentLinkResultDto(
            response.OrderCode,
            response.CheckoutUrl,
            response.PaymentLinkId,
            response.Status.ToString(),
            response.QrCode
        );
    }

    public async Task<VerifyWebhookResultDto> VerifyWebhookDataAsync(
        string rawJsonBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webhookBody = JsonSerializer.Deserialize<Webhook>(rawJsonBody, options);

            if (webhookBody == null)
            {
                return new VerifyWebhookResultDto(false, null, rawJsonBody, "Không thể deserialize dữ liệu Webhook");
            }

            var verifiedData = await _payOs.Webhooks.VerifyAsync(webhookBody);
            if (verifiedData == null)
            {
                return new VerifyWebhookResultDto(false, null, rawJsonBody, "Xác minh chữ ký Webhook thất bại");
            }

            var dataDto = new PayOsWebhookDataDto(
                verifiedData.OrderCode,
                verifiedData.Amount,
                verifiedData.Description,
                verifiedData.AccountNumber,
                verifiedData.Reference,
                verifiedData.TransactionDateTime,
                verifiedData.CounterAccountBankName,
                verifiedData.CounterAccountName,
                verifiedData.CounterAccountNumber
            );

            return new VerifyWebhookResultDto(true, dataDto, rawJsonBody, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xác minh Webhook PayOS: {Message}", ex.Message);
            return new VerifyWebhookResultDto(false, null, rawJsonBody, ex.Message);
        }
    }

    public async Task<PaymentLinkResultDto?> GetPaymentLinkInformationAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var paymentInfo = await _payOs.PaymentRequests.GetAsync(orderCode.ToString());
            if (paymentInfo == null) return null;

            return new PaymentLinkResultDto(
                paymentInfo.OrderCode,
                null,
                paymentInfo.Id,
                paymentInfo.Status.ToString(),
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy thông tin link thanh toán cho OrderCode: {OrderCode}", orderCode);
            return null;
        }
    }
}
