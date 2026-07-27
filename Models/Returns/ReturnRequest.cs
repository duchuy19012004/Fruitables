using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnRequest
{
    public int Id { get; set; }
    [Required, MaxLength(32)] public string ReturnNumber { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IdempotencyKey { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Submitted;
    public ReturnResolutionType Resolution { get; set; }
    public int PolicyVersion { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime ClaimDeadlineAtUtc { get; set; }
    public DateTime ReviewDueAtUtc { get; set; }
    public DateTime? EvidenceDueAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int? ReviewerId { get; set; }
    [MaxLength(2000)] public string? CustomerNote { get; set; }
    [MaxLength(2000)] public string? InternalNote { get; set; }
    [MaxLength(1000)] public string? DecisionReason { get; set; }
    public bool MerchantFault { get; set; }
    public bool ShippingFeeApproved { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }

    public Order Order { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? Reviewer { get; set; }
    public ICollection<ReturnRequestItem> Items { get; set; } = new List<ReturnRequestItem>();
    public ICollection<ReturnEvidence> Evidences { get; set; } = new List<ReturnEvidence>();
    public ICollection<ReturnEvent> Events { get; set; } = new List<ReturnEvent>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
