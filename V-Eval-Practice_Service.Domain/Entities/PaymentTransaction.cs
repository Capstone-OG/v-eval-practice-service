using System;

namespace V_Eval_Practice_Service.Domain.Entities;

/// <summary>
/// Nhật ký giao dịch và Webhook IPN từ PayOS phục vụ kiểm toán tài chính (Audit Logs)
/// Schema: v_eval_enrollment."PaymentTransactions"
/// </summary>
public class PaymentTransaction
{
    public Guid TransactionId { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public string EventType { get; set; } = string.Empty; // WEBHOOK_RECEIVED, STATUS_UPDATED, SIGNATURE_VERIFIED, TRANSACTION_FAILED
    public string? RawPayload { get; set; }
    public string? Signature { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    // Navigation Property
    public virtual Payment Payment { get; set; } = null!;
}

