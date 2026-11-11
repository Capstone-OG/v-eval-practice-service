using System;
using System.Collections.Generic;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Đại diện cho giao dịch thanh toán học phí (tích hợp cổng PayOS / VietQR)
/// Schema: v_eval_enrollment."Payments"
/// </summary>
public class Payment
{
    public Guid PaymentId { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public Guid ParentId { get; set; }
    public string PaymentType { get; set; } = "TUITION";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "PAYOS";
    public string Status { get; set; } = "PENDING"; // PENDING, PROCESSING, SUCCESS, FAILED, REFUNDED
    public string? TransactionRef { get; set; }

    // PayOS Integration fields
    public long? OrderCode { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? CounterAccountBankName { get; set; }
    public string? CounterAccountName { get; set; }
    public string? CounterAccountNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual CourseRegistration? Registration { get; set; }
    public virtual ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}

