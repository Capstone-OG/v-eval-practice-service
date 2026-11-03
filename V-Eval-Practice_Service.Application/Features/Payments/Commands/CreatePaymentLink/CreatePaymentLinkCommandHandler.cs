using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.CreatePaymentLink;

public class CreatePaymentLinkCommandHandler : IRequestHandler<CreatePaymentLinkCommand, Result<CreatePaymentLinkResponseDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPayOsPaymentService _payOsPaymentService;
    private readonly ILogger<CreatePaymentLinkCommandHandler> _logger;

    public CreatePaymentLinkCommandHandler(
        IPaymentRepository paymentRepository,
        IPayOsPaymentService payOsPaymentService,
        ILogger<CreatePaymentLinkCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _payOsPaymentService = payOsPaymentService;
        _logger = logger;
    }

    public async Task<Result<CreatePaymentLinkResponseDto>> Handle(
        CreatePaymentLinkCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Khởi tạo yêu cầu thanh toán khóa học {CourseId} cho học sinh {StudentId}",
            request.CourseId, request.StudentId);

        // 1. Kiểm tra khóa học có tồn tại và đang mở không
        var course = await _paymentRepository.GetCourseByIdAsync(request.CourseId, cancellationToken);
        if (course == null || !course.IsActive)
        {
            return Result<CreatePaymentLinkResponseDto>.Failure(
                Error.NotFound("Course.NotFound", $"Khóa học {request.CourseId} không tồn tại hoặc đã ngừng hoạt động."));
        }

        // 2. Kiểm tra xem học sinh đã có đăng ký ACTIVE cho khóa học này chưa
        var existingActive = await _paymentRepository.GetActiveRegistrationAsync(request.StudentId, request.CourseId, cancellationToken);
        if (existingActive != null && existingActive.Status == "ACTIVE")
        {
            return Result<CreatePaymentLinkResponseDto>.Failure(
                Error.Conflict("Registration.AlreadyActive", $"Học sinh đã có đăng ký hoạt động cho khóa học {course.Name}."));
        }

        // 3. Tạo bản ghi CourseRegistration nếu chưa có, hoặc cập nhật bản ghi đang chờ thanh toán
        var registration = new CourseRegistration
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            Status = "PENDING_PAYMENT",
            RegisteredAt = DateTime.UtcNow
        };
        await _paymentRepository.AddRegistrationAsync(registration, cancellationToken);

        // 4. Sinh orderCode an toàn theo chuẩn JavaScript (< 9007199254740991)
        long orderCode = Random.Shared.NextInt64(1000000000, 9007199254740991);

        // 5. Rút gọn nội dung mô tả chuyển khoản tối đa 25 ký tự không dấu
        var sanitizedCourseCode = string.IsNullOrWhiteSpace(course.Code) ? "CRS" : course.Code.Replace(" ", "");
        var description = $"VEVAL {sanitizedCourseCode} {orderCode}";
        if (description.Length > 25)
        {
            description = description.Substring(0, 25);
        }

        // 6. Tạo entity Payment
        var payment = new Payment
        {
            RegistrationId = registration.RegistrationId,
            ParentId = request.ParentId,
            PaymentType = "TUITION",
            Amount = course.TuitionFee,
            Method = "PAYOS",
            Status = "PENDING",
            OrderCode = orderCode,
            CreatedAt = DateTime.UtcNow
        };

        // 7. Gọi PayOS tạo Payment Link VietQR
        try
        {
            var payOsResult = await _payOsPaymentService.CreatePaymentLinkAsync(
                orderCode,
                course.TuitionFee,
                description,
                request.ReturnUrl,
                request.CancelUrl,
                cancellationToken);

            payment.CheckoutUrl = payOsResult.CheckoutUrl;
            payment.PaymentLinkId = payOsResult.PaymentLinkId;

            await _paymentRepository.AddPaymentAsync(payment, cancellationToken);
            await _paymentRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Tạo Payment Link PayOS thành công cho orderCode: {OrderCode}, CheckoutUrl: {Url}",
                orderCode, payment.CheckoutUrl);

            return Result<CreatePaymentLinkResponseDto>.Success(new CreatePaymentLinkResponseDto(
                payment.PaymentId,
                orderCode,
                registration.RegistrationId,
                payment.Amount,
                payment.CheckoutUrl ?? string.Empty,
                payment.Status
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi cổng thanh toán PayOS tạo link cho orderCode: {OrderCode}", orderCode);
            return Result<CreatePaymentLinkResponseDto>.Failure(
                Error.Failure("PayOS.CreateLinkFailed", $"Không thể tạo link thanh toán PayOS: {ex.Message}"));
        }
    }
}

