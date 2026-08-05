using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnRequest
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

    [MaxLength(4000)]
    public string? CustomerNote { get; set; }

    [MaxLength(4000)]
    public string? AdminNote { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<ReturnRequestItem> Items { get; set; } = new List<ReturnRequestItem>();
    public virtual ICollection<ReturnEvidence> Evidence { get; set; } = new List<ReturnEvidence>();
    public virtual ICollection<ReturnEvent> Events { get; set; } = new List<ReturnEvent>();
    public virtual Refund? Refund { get; set; }
}
