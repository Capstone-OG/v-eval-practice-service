using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using V_Eval_Practice_Service.Application.Common.Interfaces;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Domain.Entities;
using V_Eval_Practice_Service.Infrastructure.Persistence;

namespace V_Eval_Practice_Service.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PracticeDbContext _context;

    public PaymentRepository(PracticeDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.Registration)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);
    }

    public async Task<Payment?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.Registration)
            .FirstOrDefaultAsync(p => p.OrderCode == orderCode, ct);
    }

    public async Task<Payment?> GetByRegistrationIdAsync(Guid registrationId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Where(p => p.RegistrationId == registrationId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Course?> GetCourseByIdAsync(Guid courseId, CancellationToken ct = default)
    {
        return await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, ct);
    }

    public async Task<CourseRegistration?> GetRegistrationByIdAsync(Guid registrationId, CancellationToken ct = default)
    {
        return await _context.CourseRegistrations
            .Include(r => r.Course)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);
    }

    public async Task<CourseRegistration?> GetActiveRegistrationAsync(Guid studentId, Guid courseId, CancellationToken ct = default)
    {
        return await _context.CourseRegistrations
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.CourseId == courseId && r.Status == "ACTIVE", ct);
    }

    public async Task<Class?> GetAvailableClassByCourseIdAsync(Guid courseId, CancellationToken ct = default)
    {
        return await _context.Classes
            .FirstOrDefaultAsync(c => c.CourseId == courseId && c.Status == "ACTIVE", ct);
    }

    public async Task AddPaymentAsync(Payment payment, CancellationToken ct = default)
    {
        await _context.Payments.AddAsync(payment, ct);
    }

    public async Task AddRegistrationAsync(CourseRegistration registration, CancellationToken ct = default)
    {
        await _context.CourseRegistrations.AddAsync(registration, ct);
    }

    public async Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken ct = default)
    {
        await _context.PaymentTransactions.AddAsync(transaction, ct);
    }

    public async Task AddClassEnrollmentAsync(ClassEnrollment enrollment, CancellationToken ct = default)
    {
        await _context.ClassEnrollments.AddAsync(enrollment, ct);
    }

    public async Task<IReadOnlyList<PaymentTransaction>> GetTransactionsByPaymentIdAsync(Guid paymentId, CancellationToken ct = default)
    {
        return await _context.PaymentTransactions
            .Where(t => t.PaymentId == paymentId)
            .OrderByDescending(t => t.LoggedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Payment>> GetPaymentsByStudentIdAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.Registration)
            .Where(p => p.Registration != null && p.Registration.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IExecutionTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var efTransaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfExecutionTransaction(efTransaction);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    private sealed class EfExecutionTransaction : IExecutionTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfExecutionTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.RollbackAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}

