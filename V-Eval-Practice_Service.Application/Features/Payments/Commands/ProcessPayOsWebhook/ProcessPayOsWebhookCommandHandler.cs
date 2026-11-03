using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Interfaces.Services;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Payments.Commands.ProcessPayOsWebhook;

public class ProcessPayOsWebhookCommandHandler : IRequestHandler<ProcessPayOsWebhookCommand, Result<bool>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILearningRoadmapRepository _roadmapRepository;
    private readonly IPayOsPaymentService _payOsPaymentService;
    private readonly ILogger<ProcessPayOsWebhookCommandHandler> _logger;

    public ProcessPayOsWebhookCommandHandler(
        IPaymentRepository paymentRepository,
        ILearningRoadmapRepository roadmapRepository,
        IPayOsPaymentService payOsPaymentService,
        ILogger<ProcessPayOsWebhookCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _roadmapRepository = roadmapRepository;
        _payOsPaymentService = payOsPaymentService;
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

        // 2. Mở Transaction ACID để đảm bảo tính toàn vẹn tuyệt đối trên PostgreSQL
        await using var transaction = await _paymentRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            // 3. Tra cứu Payment theo OrderCode
            var payment = await _paymentRepository.GetByOrderCodeAsync(webhookData.OrderCode, cancellationToken);
            if (payment == null)
            {
                _logger.LogWarning("Không tìm thấy bản ghi Payment với OrderCode {OrderCode}", webhookData.OrderCode);
                return Result<bool>.Failure(Error.NotFound("Payment.NotFound", $"Không tìm thấy đơn hàng {webhookData.OrderCode}."));
            }

            // 4. Idempotency Check: Nếu đơn hàng đã hoàn tất trước đó thì bỏ qua xử lý trùng
            if (payment.Status == "SUCCESS")
            {
                _logger.LogInformation("Đơn hàng {OrderCode} đã được xử lý SUCCESS trước đó (Idempotent bypass)", webhookData.OrderCode);
                await transaction.CommitAsync(cancellationToken);
                return Result<bool>.Success(true);
            }

            // 5. Cập nhật thông tin giao dịch Payment
            payment.Status = "SUCCESS";
            payment.PaidAt = DateTime.UtcNow;
            payment.TransactionRef = webhookData.Reference;
            payment.CounterAccountBankName = string.IsNullOrWhiteSpace(webhookData.CounterAccountBankName) ? "MOMO / VIETQR" : webhookData.CounterAccountBankName;
            payment.CounterAccountName = webhookData.CounterAccountName;
            payment.CounterAccountNumber = webhookData.CounterAccountNumber;

            // 6. Kích hoạt CourseRegistration
            var registration = await _paymentRepository.GetRegistrationByIdAsync(payment.RegistrationId, cancellationToken);
            if (registration != null)
            {
                registration.Status = "ACTIVE";
                registration.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
                var course = await _paymentRepository.GetCourseByIdAsync(registration.CourseId, cancellationToken);
                int durationWeeks = course?.DurationWeeks ?? 12;
                registration.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(durationWeeks * 7));

                // 7. Tự động xếp lớp (ClassEnrollment) vào lớp học phù hợp còn chỗ
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
                    _logger.LogInformation("Đã tự động xếp học sinh {StudentId} vào lớp {ClassId}", registration.StudentId, availableClass.ClassId);
                }

                // 8. Tự động khởi tạo hoặc mở khóa Lộ trình học thích ứng PLAR (LearningRoadmap & RoadmapNodes)
                var existingRoadmap = await _roadmapRepository.GetActiveByStudentIdAsync(registration.StudentId, cancellationToken);
                if (existingRoadmap == null)
                {
                    var newRoadmap = new LearningRoadmap
                    {
                        StudentId = registration.StudentId,
                        TargetScore = 800,
                        Status = "ACTIVE",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Khởi tạo 4 chặng học cơ bản chu trình PLAR (Prerequisite, Learn, Adaptive Practice, Review)
                    for (int step = 1; step <= 4; step++)
                    {
                        newRoadmap.Nodes.Add(new RoadmapNode
                        {
                            RoadmapId = newRoadmap.RoadmapId,
                            SkillId = Guid.NewGuid(),
                            StepOrder = step,
                            Status = (step == 1) ? "IN_PROGRESS" : "LOCKED",
                            UnlockedAt = (step == 1) ? DateTime.UtcNow : null
                        });
                    }
                    newRoadmap.TotalMilestones = newRoadmap.Nodes.Count;
                    newRoadmap.CompletedMilestones = 0;

                    await _roadmapRepository.AddAsync(newRoadmap, cancellationToken);
                    _logger.LogInformation("Đã khởi tạo Lộ trình học thích ứng PLAR cho học sinh {StudentId}", registration.StudentId);
                }
            }

            // 9. Ghi Audit Log vào PaymentTransactions
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

            // 10. Lưu tất cả thay đổi & Commit Transaction đồng bộ
            await _paymentRepository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Xử lý Webhook PayOS thành công trọn vẹn (ACID) cho đơn hàng {OrderCode}", webhookData.OrderCode);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý Transaction Webhook PayOS cho OrderCode {OrderCode}. Tiến hành Rollback.", webhookData.OrderCode);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

