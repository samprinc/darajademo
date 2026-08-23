namespace DarajaDemo.Data.Entities;

public enum TransactionSource { STK_PUSH, C2B_MANUAL }
public enum TransactionStatus { PENDING, COMPLETED, FAILED }

public class MpesaTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TransactionSource Source { get; set; }
    public string? MerchantRequestId { get; set; }
    public string? CheckoutRequestId { get; set; }
    public string? TransId { get; set; } // M-Pesa Receipt Number
    public string? PhoneNumber { get; set; }
    public decimal Amount { get; set; }
    public string? BillRefNumber { get; set; } // Account Ref
    public TransactionStatus Status { get; set; }
    public int? ResultCode { get; set; }
    public string? ResultDesc { get; set; }
    public string? RawPayloadJson { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}