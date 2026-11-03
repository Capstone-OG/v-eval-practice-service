using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using V_Eval_Practice_Service.Domain.Entities;

namespace V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository quản lý dữ liệu Khóa học, Đăng ký, Thanh toán và Nhật ký giao dịch
/// </summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct = default);
    Task<Payment?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default);
    Task<Payment?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct = default);
    
    Task<Course?> GetCourseByIdAsync(Guid courseId, CancellationToken ct = default);
    Task<CourseRegistration?> GetRegistrationByIdAsync(Guid registrationId, CancellationToken ct = default);
    Task<CourseRegistration?> GetActiveRegistrationAsync(Guid studentId, Guid courseId, CancellationToken ct = default);
    Task<Class?> GetAvailableClassByCourseIdAsync(Guid courseId, CancellationToken ct = default);

    Task AddPaymentAsync(Payment payment, CancellationToken ct = default);
    Task AddRegistrationAsync(CourseRegistration registration, CancellationToken ct = default);
    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default);
    Task AddClassEnrollmentAsync(ClassEnrollment enrollment, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentTransaction>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetPaymentsByStudentIdAsync(Guid studentId, CancellationToken ct = default);

    Task<IExecutionTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

