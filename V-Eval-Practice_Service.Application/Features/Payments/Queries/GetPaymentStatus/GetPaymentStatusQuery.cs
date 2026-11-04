using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Payments.Queries.GetPaymentStatus;

public record GetPaymentStatusQuery(long OrderCode) : IRequest<Result<PaymentStatusResponseDto>>;

public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, Result<PaymentStatusResponseDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayOsPaymentService _payOsPaymentService;

    public GetPaymentStatusQueryHandler(
        IPaymentRepository paymentRepository,
        IPayOsPaymentService payOsPaymentService)
    {
        _paymentRepository = paymentRepository;
        _payOsPaymentService = payOsPaymentService;
    }

    public async Task<Result<PaymentStatusResponseDto>> Handle(
        GetPaymentStatusQuery request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByOrderCodeAsync(request.OrderCode, cancellationToken);
        if (payment == null)
        {
            return Result<PaymentStatusResponseDto>.Failure(
                Error.NotFound("Payment.NotFound", $"Không tìm thấy thông tin thanh toán cho đơn hàng {request.OrderCode}."));
        }

        var registration = await _paymentRepository.GetRegistrationByIdAsync(payment.RegistrationId, cancellationToken);
        var course = registration != null ? await _paymentRepository.GetCourseByIdAsync(registration.CourseId, cancellationToken) : null;

        // Nếu trạng thái vẫn đang PENDING, kiểm tra trực tiếp với cổng PayOS đề phòng Webhook bị delay
        if (payment.Status == "PENDING" && payment.OrderCode.HasValue)
        {
            try
            {
                var payOsInfo = await _payOsPaymentService.GetPaymentLinkInformationAsync(payment.OrderCode.Value, cancellationToken);
                if (payOsInfo != null && payOsInfo.Status == "PAID" && payment.Status != "SUCCESS")
                {
                    payment.Status = "SUCCESS";
                    payment.PaidAt = DateTime.UtcNow;
                    if (registration != null)
                    {
                        registration.Status = "ACTIVE";
                    }
                    await _paymentRepository.SaveChangesAsync(cancellationToken);
                }
                else if (payOsInfo != null && (payOsInfo.Status == "CANCELLED" || payOsInfo.Status == "EXPIRED"))
                {
                    payment.Status = "FAILED";
                    await _paymentRepository.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
                // Fallback nếu không gọi được PayOS API lúc đó
            }
        }

        return Result<PaymentStatusResponseDto>.Success(new PaymentStatusResponseDto(
            payment.PaymentId,
            payment.OrderCode,
            payment.RegistrationId,
            registration?.StudentId ?? Guid.Empty,
            registration?.CourseId ?? Guid.Empty,
            course?.Name ?? "Khóa học",
            payment.Amount,
            payment.Status,
            payment.CheckoutUrl,
            payment.PaidAt,
            payment.CounterAccountBankName,
            payment.CounterAccountName,
            payment.CounterAccountNumber
        ));
    }
}

