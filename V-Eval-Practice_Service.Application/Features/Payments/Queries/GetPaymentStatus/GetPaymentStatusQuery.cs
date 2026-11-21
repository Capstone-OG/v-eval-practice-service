using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Payments.Queries.GetPaymentStatus;

public record GetPaymentStatusQuery(long OrderCode) : IRequest<Result<PaymentStatusResponseDto>>;

public class GetPaymentStatusQueryHandler : IRequestHandler<GetPaymentStatusQuery, Result<PaymentStatusResponseDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IPayOsPaymentService _payOsPaymentService;
    private readonly ISender _mediator;

    public GetPaymentStatusQueryHandler(
        IPaymentRepository paymentRepository,
        ILearningRoadmapRepository roadmapRepository,
        IExamSubmissionRepository submissionRepository,
        IPayOsPaymentService payOsPaymentService,
        ISender mediator)
    {
        _paymentRepository = paymentRepository;
        _roadmapRepository = roadmapRepository;
        _submissionRepository = submissionRepository;
        _payOsPaymentService = payOsPaymentService;
        _mediator = mediator;
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
                        registration.StartDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
                        int durationWeeks = course?.DurationWeeks ?? 12;
                        registration.EndDate ??= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(durationWeeks * 7));

                        var availableClass = await _paymentRepository.GetAvailableClassByCourseIdAsync(registration.CourseId, cancellationToken);
                        if (availableClass != null)
                        {
                            var existingEnroll = await _paymentRepository.GetEnrollmentByStudentIdAsync(registration.StudentId, cancellationToken);
                            if (existingEnroll == null)
                            {
                                await _paymentRepository.AddClassEnrollmentAsync(new ClassEnrollment
                                {
                                    ClassId = availableClass.ClassId,
                                    StudentId = registration.StudentId,
                                    RegistrationId = registration.RegistrationId,
                                    Status = "ENROLLED",
                                    EnrolledAt = DateTime.UtcNow
                                }, cancellationToken);
                            }
                        }
                    }

                    await _paymentRepository.AddTransactionAsync(new PaymentTransaction
                    {
                        PaymentId = payment.PaymentId,
                        EventType = "POLLING_VERIFIED_PAID",
                        RawPayload = $"{{\"orderCode\":{payment.OrderCode},\"status\":\"PAID\"}}",
                        Signature = "PAYOS_API_POLL",
                        IpAddress = "server-poll",
                        LoggedAt = DateTime.UtcNow
                    }, cancellationToken);

                    await _paymentRepository.SaveChangesAsync(cancellationToken);

                    // Đồng bộ sinh lộ trình thật từ Diagnostic nếu học sinh chưa có lộ trình
                    if (registration != null)
                    {
                        var existingRoadmap = await _roadmapRepository.GetActiveByStudentIdAsync(registration.StudentId, cancellationToken);
                        if (existingRoadmap == null)
                        {
                            var submissions = await _submissionRepository.GetByStudentIdAsync(registration.StudentId, cancellationToken);
                            var latestDiag = submissions
                                .Where(s => s.ExamType == "DIAGNOSTIC" && s.Status == "COMPLETED")
                                .OrderByDescending(s => s.CompletedAt)
                                .FirstOrDefault();

                            if (latestDiag != null)
                            {
                                await _mediator.Send(new GenerateRoadmapCommand(
                                    StudentId: registration.StudentId,
                                    DiagnosticSubmissionId: latestDiag.SubmissionId,
                                    ExamDate: DateTime.UtcNow.AddDays(90),
                                    StudyHoursPerDay: 2.0
                                ), cancellationToken);
                            }
                        }
                    }
                }
                else if (payOsInfo != null && (payOsInfo.Status == "CANCELLED" || payOsInfo.Status == "EXPIRED"))
                {
                    payment.Status = "FAILED";
                    await _paymentRepository.SaveChangesAsync(cancellationToken);
                }
            }
            catch
            {
                // Fallback nếu tạm thời không gọi được PayOS API
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
