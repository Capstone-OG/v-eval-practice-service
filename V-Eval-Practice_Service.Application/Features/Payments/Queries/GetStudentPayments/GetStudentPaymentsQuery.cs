using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;
using V_Eval_Practice_Service.Application.Features.Payments.DTOs;

namespace V_Eval_Practice_Service.Application.Features.Payments.Queries.GetStudentPayments;

public record GetStudentPaymentsQuery(Guid StudentId) : IRequest<Result<IReadOnlyList<StudentPaymentItemDto>>>;

public class GetStudentPaymentsQueryHandler : IRequestHandler<GetStudentPaymentsQuery, Result<IReadOnlyList<StudentPaymentItemDto>>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetStudentPaymentsQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<StudentPaymentItemDto>>> Handle(
        GetStudentPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.GetPaymentsByStudentIdAsync(request.StudentId, cancellationToken);

        var result = payments.Select(p => new StudentPaymentItemDto(
            p.PaymentId,
            p.OrderCode,
            p.RegistrationId,
            p.Registration?.CourseId ?? Guid.Empty,
            p.Registration?.Course?.Name ?? "Khóa Luyện Thi Chuẩn (Standard Adaptive)",
            p.Registration?.Course?.Code ?? "CRS_STD",
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

        return Result<IReadOnlyList<StudentPaymentItemDto>>.Success(result);
    }
}

