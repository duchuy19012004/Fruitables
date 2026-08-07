using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class Refund
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int OrderId { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal ShippingFeeAmount { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    [MaxLength(2000)]
    public string? FailureReason { get; set; }

    public int CreatedByUserId { get; set; }
    public int? ProcessedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }

    public virtual ReturnRequest ReturnRequest { get; set; } = null!;
    public virtual Order Order { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual User? ProcessedByUser { get; set; }
}
