using System;

namespace V_Eval_Practice_Service.Application.Features.Payments.DTOs;

public record PaymentLinkResultDto(
    long OrderCode,
    string? CheckoutUrl,
    string? PaymentLinkId,
    string Status,
    string? QrCode
);

public record PayOsWebhookDataDto(
    long OrderCode,
    long Amount,
    string Description,
    string AccountNumber,
    string Reference,
    string TransactionDateTime,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber
);

public record VerifyWebhookResultDto(
    bool IsValid,
    PayOsWebhookDataDto? Data,
    string? RawPayload,
    string? ErrorReason
);

public record PaymentStatusResponseDto(
    Guid PaymentId,
    long? OrderCode,
    Guid RegistrationId,
    Guid StudentId,
    Guid CourseId,
    string CourseName,
    decimal Amount,
    string Status,
    string? CheckoutUrl,
    DateTime? PaidAt,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber
);

public record CreatePaymentLinkResponseDto(
    Guid PaymentId,
    long OrderCode,
    Guid RegistrationId,
    decimal Amount,
    string CheckoutUrl,
    string Status
);
