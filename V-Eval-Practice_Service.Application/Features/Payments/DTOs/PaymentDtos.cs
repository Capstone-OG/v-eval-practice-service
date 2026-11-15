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

public record StudentPaymentItemDto(
    Guid PaymentId,
    long? OrderCode,
    Guid RegistrationId,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    decimal Amount,
    string Status,
    string PaymentType,
    string Method,
    string? TransactionRef,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber,
    DateTime? PaidAt,
    DateTime CreatedAt
);

public record StudentTuitionStatusDto(
    Guid StudentId,
    bool IsPaid,
    Guid? ActiveRegistrationId,
    Guid? ActiveCourseId,
    string? CourseName,
    string? CourseCode,
    string? PlanType,
    int DurationWeeks,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? AssignedClassId,
    string? AssignedClassName,
    Guid? CampusId,
    IReadOnlyList<string> UnlockedFeatures,
    IReadOnlyList<StudentPaymentItemDto> Payments
);
