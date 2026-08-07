using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public enum SePayTransactionStatus
{
    Paid,
    Duplicate,
    Ignored
}

public class SePayTransaction
{
    public int Id { get; set; }

    public long SePayTransactionId { get; set; }

    public int? OrderId { get; set; }

    [MaxLength(16)]
    public string? PaymentCode { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TransferAmount { get; set; }

    [MaxLength(100)]
    public string? ReferenceCode { get; set; }

    public SePayTransactionStatus Status { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Order? Order { get; set; }
}
