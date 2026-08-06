using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnCase
{
    public int Id { get; set; }

    [Required, MaxLength(32)]
    public string ReturnNumber { get; set; } = string.Empty;

    public int OrderId { get; set; }
    public int UserId { get; set; }
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Submitted;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ClaimDeadlineAtUtc { get; set; }
    public DateTime? SupplementDeadlineAtUtc { get; set; }
    public int SupplementCount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal RequestedAmount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal ApprovedAmount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal ApprovedShippingFeeAmount { get; set; }

    public string DetailsJson { get; set; } = "{ \"schemaVersion\": 1 }";

    [ConcurrencyCheck]
    public byte[]? RowVersion { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
