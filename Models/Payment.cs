using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    [Required, MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProviderTransactionId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(16)]
    public string? PaymentCode { get; set; }

    [MaxLength(100)]
    public string? ReferenceCode { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    public virtual Order Order { get; set; } = null!;
}
