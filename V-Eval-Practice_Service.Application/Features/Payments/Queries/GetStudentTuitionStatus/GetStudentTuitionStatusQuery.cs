using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Features.Payments.Queries.GetStudentTuitionStatus;

public record GetStudentTuitionStatusQuery(Guid StudentId) : IRequest<Result<StudentTuitionStatusDto>>;

public class GetStudentTuitionStatusQueryHandler : IRequestHandler<GetStudentTuitionStatusQuery, Result<StudentTuitionStatusDto>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetStudentTuitionStatusQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<StudentTuitionStatusDto>> Handle(
        GetStudentTuitionStatusQuery request,
        CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.GetPaymentsByStudentIdAsync(request.StudentId, cancellationToken);
        var activeRegistration = await _paymentRepository.GetLatestActiveRegistrationByStudentIdAsync(request.StudentId, cancellationToken);
        var enrollment = await _paymentRepository.GetEnrollmentByStudentIdAsync(request.StudentId, cancellationToken);

        var hasSuccessfulPayment = payments.Any(p => p.Status == "SUCCESS" || p.Status == "PAID");
        var isPaid = activeRegistration != null || hasSuccessfulPayment;

        // Nếu có thanh toán SUCCESS mà registration chưa thành ACTIVE thì tự động đồng bộ
        if (hasSuccessfulPayment && activeRegistration == null)
        {
            var latestPayment = payments.FirstOrDefault(p => p.Status == "SUCCESS" || p.Status == "PAID");
            if (latestPayment?.Registration != null)
            {
                latestPayment.Registration.Status = "ACTIVE";
                latestPayment.Registration.StartDate = latestPayment.Registration.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var durationWeeks = latestPayment.Registration.Course?.DurationWeeks ?? 12;
                latestPayment.Registration.EndDate = latestPayment.Registration.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(durationWeeks * 7));
                await _paymentRepository.SaveChangesAsync(cancellationToken);
                activeRegistration = latestPayment.Registration;
            }
        }

        var course = activeRegistration?.Course;
        var paymentDtos = payments.Select(p => new StudentPaymentItemDto(
            p.PaymentId,
            p.OrderCode,
            p.RegistrationId,
            p.Registration?.CourseId ?? Guid.Empty,
            p.Registration?.Course?.Name ?? (course?.Name ?? "Khóa Luyện Thi Chuẩn (Standard Adaptive)"),
            p.Registration?.Course?.Code ?? (course?.Code ?? "CRS_STD"),
            p.Amount,
            p.Status,
            p.PaymentType,
            p.Method,
            p.TransactionRef,
            p.CounterAccountBankName,
            p.CounterAccountName,
            p.CounterAccountNumber,
            p.PaidAt,
            p.CreatedAt
        )).ToList();

        var unlockedFeatures = isPaid ? new List<string>
        {
            "Lộ trình học thích ứng 4 bước PLAR",
            "Toàn bộ bài giảng chuyên đề & Video độc quyền",
            "Kho luyện tập thích ứng ZPD",
            "Gia sư AI Socratic kèm cặp 24/7",
            "Thời khóa biểu & Điểm danh tại cơ sở",
            "Lớp học phần & Diễn đàn Live Q&A",
            "Khảo sát chẩn đoán & Báo cáo năng lực 5 trục"
        } : new List<string>();

        var dto = new StudentTuitionStatusDto(
            request.StudentId,
            isPaid,
            activeRegistration?.RegistrationId,
            course?.CourseId,
            course?.Name ?? (isPaid ? "Khóa Luyện Thi Chuẩn (Standard Adaptive)" : null),
            course?.Code ?? (isPaid ? "CRS_STD" : null),
            course?.PlanType ?? (isPaid ? "STANDARD" : null),
            course?.DurationWeeks ?? 12,
            activeRegistration?.StartDate ?? (isPaid ? DateOnly.FromDateTime(DateTime.UtcNow) : null),
            activeRegistration?.EndDate ?? (isPaid ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(84)) : null),
            enrollment?.ClassId,
            enrollment?.Class?.Name ?? (isPaid ? "Lớp Chuyên Đề ĐGNL 2026 - Chuẩn Đầu Ra" : null),
            course?.CampusId,
            unlockedFeatures,
            paymentDtos
        );

        return Result<StudentTuitionStatusDto>.Success(dto);
    }
}

