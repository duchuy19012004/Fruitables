using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class Refund
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int? ReturnRequestItemId { get; set; }
    public int OrderId { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal Amount { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal ShippingFeeAmount { get; set; }
    public RefundMethod Method { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public RefundFailureKind FailureKind { get; set; }
    public int FailureAttemptCount { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal FinancialSeparationThresholdSnapshot { get; set; }
    [Required, MaxLength(64)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(128)] public string? TransactionReference { get; set; }
    [MaxLength(128)] public string? TransferEvidenceStorageKey { get; set; }
    [MaxLength(1000)] public string? FailureReason { get; set; }
    [MaxLength(50)] public string? DestinationBankCode { get; set; }
    [MaxLength(1000)] public string? DestinationAccountNumberProtected { get; set; }
    [MaxLength(4)] public string? DestinationAccountLast4 { get; set; }
    [MaxLength(1000)] public string? DestinationAccountHolderProtected { get; set; }
    public DateTime? DestinationSubmittedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ProcessedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public ReturnRequestItem? ReturnRequestItem { get; set; }
    public Order Order { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ProcessedByUser { get; set; }
}
