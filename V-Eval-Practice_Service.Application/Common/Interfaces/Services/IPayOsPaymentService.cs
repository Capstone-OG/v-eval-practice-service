using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Services;

/// <summary>
/// Giao diện tương tác với Cổng thanh toán trực tuyến PayOS (VietQR)
/// </summary>
public interface IPayOsPaymentService
{
    Task<PaymentLinkResultDto> CreatePaymentLinkAsync(
        long orderCode,
        decimal amount,
        string description,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<VerifyWebhookResultDto> VerifyWebhookDataAsync(
        string rawJsonBody,
        CancellationToken cancellationToken = default);

    Task<PaymentLinkResultDto?> GetPaymentLinkInformationAsync(
        long orderCode,
        CancellationToken cancellationToken = default);
}

