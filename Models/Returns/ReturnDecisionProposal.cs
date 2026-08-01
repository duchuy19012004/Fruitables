using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models.Returns;

public class ReturnDecisionProposal
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int Version { get; set; }
    public ReturnDecisionProposalStatus Status { get; set; } = ReturnDecisionProposalStatus.Draft;
    public int ProposedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal AggregateAmount { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal ShippingFeeAmount { get; set; }
    public bool ShippingFeeEligibilitySnapshot { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public User ProposedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public ICollection<ReturnDecisionProposalItem> Items { get; set; } = new List<ReturnDecisionProposalItem>();
}
