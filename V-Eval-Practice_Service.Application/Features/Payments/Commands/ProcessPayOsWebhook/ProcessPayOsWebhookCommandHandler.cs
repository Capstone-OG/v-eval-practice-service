using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Roadmaps.Commands.GenerateRoadmap;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.ProcessPayOsWebhook;

public class ProcessPayOsWebhookCommandHandler : IRequestHandler<ProcessPayOsWebhookCommand, Result<bool>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IExamSubmissionRepository _submissionRepository;
    private readonly IPayOsPaymentService _payOsPaymentService;
    private readonly ISender _mediator;
    private readonly ILogger<ProcessPayOsWebhookCommandHandler> _logger;

    public ProcessPayOsWebhookCommandHandler(
        IPaymentRepository paymentRepository,
        ILearningRoadmapRepository roadmapRepository,
        IExamSubmissionRepository submissionRepository,
        IPayOsPaymentService payOsPaymentService,
        ISender mediator,
        ILogger<ProcessPayOsWebhookCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _roadmapRepository = roadmapRepository;
        _submissionRepository = submissionRepository;
        _payOsPaymentService = payOsPaymentService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(ProcessPayOsWebhookCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Nhận Webhook IPN từ PayOS...");

        // 1. Kiểm tra chữ ký số HMAC-SHA512 qua service
        var verifyResult = await _payOsPaymentService.VerifyWebhookDataAsync(request.RawPayload, cancellationToken);
        if (!verifyResult.IsValid || verifyResult.Data == null)
        {
            _logger.LogWarning("Chữ ký Webhook PayOS không hợp lệ hoặc dữ liệu sai: {Reason}", verifyResult.ErrorReason);
            return Result<bool>.Failure(Error.Validation("PayOS.InvalidSignature", verifyResult.ErrorReason ?? "Chữ ký số không khớp."));
        }

        var webhookData = verifyResult.Data;
        _logger.LogInformation("Webhook PayOS xác thực chữ ký thành công cho OrderCode: {OrderCode}", webhookData.OrderCode);

        Guid? activatedStudentId = null;

        // 2. Mở Transaction ACID để đảm bảo tính toàn vẹn tuyệt đối trên PostgreSQL
        await using var transaction = await _paymentRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(webhookData.OrderCode, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning("Không tìm thấy bản ghi Payment với OrderCode {OrderCode}", webhookData.OrderCode);
                return Result<bool>.Failure(Error.NotFound("Payment.NotFound", $"Không tìm thấy đơn hàng {webhookData.OrderCode}."));
            }

            if (payment.Status == "SUCCESS")
            {
                _logger.LogInformation("Đơn hàng {OrderCode} đã được xử lý SUCCESS trước đó (Idempotent bypass)", webhookData.OrderCode);
                await transaction.CommitAsync(cancellationToken);
                return Result<bool>.Success(true);
            }

            payment.Status = "SUCCESS";
            payment.PaidAt = DateTime.UtcNow;
            payment.TransactionRef = webhookData.Reference;
            payment.CounterAccountBankName = string.IsNullOrWhiteSpace(webhookData.CounterAccountBankName) ? "VIETQR" : webhookData.CounterAccountBankName;
            payment.CounterAccountName = webhookData.CounterAccountName;
            payment.CounterAccountNumber = webhookData.CounterAccountNumber;

            var registration = await _paymentRepository.GetRegistrationByIdAsync(payment.RegistrationId, cancellationToken);
            if (registration != null)
            {
                registration.Status = "ACTIVE";
                registration.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
                var course = await _paymentRepository.GetCourseByIdAsync(registration.CourseId, cancellationToken);
                int durationWeeks = course?.DurationWeeks ?? 12;
                registration.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(durationWeeks * 7));
                activatedStudentId = registration.StudentId;

                var availableClass = await _paymentRepository.GetAvailableClassByCourseIdAsync(registration.CourseId, cancellationToken);
                if (availableClass != null)
                {
                    var enrollment = new ClassEnrollment
                    {
                        ClassId = availableClass.ClassId,
                        StudentId = registration.StudentId,
                        RegistrationId = registration.RegistrationId,
                        Status = "ENROLLED",
                        EnrolledAt = DateTime.UtcNow
                    };
                    await _paymentRepository.AddClassEnrollmentAsync(enrollment, cancellationToken);
                }
            }

            var auditTxn = new PaymentTransaction
            {
                PaymentId = payment.PaymentId,
                EventType = "WEBHOOK_PROCESSED_SUCCESS",
                RawPayload = request.RawPayload,
                Signature = "HMAC-SHA512-VERIFIED",
                IpAddress = request.IpAddress,
                LoggedAt = DateTime.UtcNow
            };
            await _paymentRepository.AddTransactionAsync(auditTxn, cancellationToken);

            await _paymentRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Xử lý Webhook PayOS thành công trọn vẹn (ACID) cho đơn hàng {OrderCode}", webhookData.OrderCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý Transaction Webhook PayOS cho OrderCode {OrderCode}. Tiến hành Rollback.", webhookData.OrderCode);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Sau khi commit thanh toán thành công: Tự động sinh Lộ trình cá nhân hóa THẬT từ kết quả Diagnostic (KHÔNG tạo 4 node Guid.NewGuid giả!)
        if (activatedStudentId.HasValue)
        {
            try
            {
                var existingRoadmap = await _roadmapRepository.GetActiveByStudentIdAsync(activatedStudentId.Value, cancellationToken);
                if (existingRoadmap == null)
                {
                    var submissions = await _submissionRepository.GetByStudentIdAsync(activatedStudentId.Value, cancellationToken);
                    var latestDiag = submissions
                        .Where(s => s.ExamType == "DIAGNOSTIC" && s.Status == "COMPLETED")
                        .OrderByDescending(s => s.CompletedAt)
                        .FirstOrDefault();

                    if (latestDiag != null)
                    {
                        await _mediator.Send(new GenerateRoadmapCommand(
                            StudentId: activatedStudentId.Value,
                            DiagnosticSubmissionId: latestDiag.SubmissionId,
                            ExamDate: DateTime.UtcNow.AddDays(90),
                            StudyHoursPerDay: 2.0
                        ), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Thanh toán đã thành công nhưng chưa thể tự sinh lộ trình sau webhook cho học sinh {StudentId}", activatedStudentId.Value);
            }
        }

        return Result<bool>.Success(true);
    }
}
