using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using V_Eval_Practice_Service.Application.Common.Interfaces.Repositories;
using V_Eval_Practice_Service.Application.Common.Models;

namespace V_Eval_Practice_Service.Application.Features.Payments.Queries.GetPaymentTransactions;

public record PaymentTransactionItemDto(
    Guid TransactionId,
    Guid PaymentId,
    string EventType,
    string? RawPayload,
    string? Signature,
    string? IpAddress,
    DateTime LoggedAt
);

public record GetPaymentTransactionsQuery(Guid PaymentId) : IRequest<Result<IReadOnlyList<PaymentTransactionItemDto>>>;

public class GetPaymentTransactionsQueryHandler : IRequestHandler<GetPaymentTransactionsQuery, Result<IReadOnlyList<PaymentTransactionItemDto>>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetPaymentTransactionsQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<PaymentTransactionItemDto>>> Handle(
        GetPaymentTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            return Result<IReadOnlyList<PaymentTransactionItemDto>>.Failure(
                Error.NotFound("Payment.NotFound", $"Không tìm thấy giao dịch {request.PaymentId}."));
        }

        var transactions = await _paymentRepository.GetTransactionsByPaymentIdAsync(request.PaymentId, cancellationToken);

        var dtos = transactions.Select(t => new PaymentTransactionItemDto(
            t.TransactionId,
            t.PaymentId,
            t.EventType,
            t.RawPayload,
            t.Signature,
            t.IpAddress,
            t.LoggedAt
        )).ToList();

        return Result<IReadOnlyList<PaymentTransactionItemDto>>.Success(dtos);
    }
}

