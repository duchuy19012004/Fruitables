using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnRequestItem
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public int? ReturnPolicyId { get; set; }
    public int RequestedQuantity { get; set; }
    public int ApprovedQuantity { get; set; }
    public ReturnItemDecisionStatus Status { get; set; } = ReturnItemDecisionStatus.Submitted;
    public ReturnReasonCode Reason { get; set; }
    public ReturnResolutionType RequestedResolution { get; set; }
    public ReturnDamagePercentage DamagePercentageRequested { get; set; } = ReturnDamagePercentage.Full;
    public ReturnDamagePercentage DamagePercentageApproved { get; set; }
    public ReturnCauseCode Cause { get; set; } = ReturnCauseCode.Unknown;
    public ReturnCostBearer CostBearer { get; set; } = ReturnCostBearer.Unknown;
    [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string? DecisionReason { get; set; }
    public int AppealCount { get; set; }
    public DateTime? AppealDeadlineAtUtc { get; set; }
    public int? CurrentDecisionProposalId { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal NetPaidAmountSnapshot { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal RequestedAmount { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal ApprovedAmount { get; set; }
    public int PolicyVersionSnapshot { get; set; }
    public int ClaimWindowHoursSnapshot { get; set; }
    public bool EvidenceRequiredSnapshot { get; set; }
    public DateTime ClaimDeadlineAtUtcSnapshot { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
    public ReturnPolicy? ReturnPolicy { get; set; }
    public ICollection<ReturnEvidence> Evidences { get; set; } = new List<ReturnEvidence>();
    public ICollection<ReturnEvidenceLink> EvidenceLinks { get; set; } = new List<ReturnEvidenceLink>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
    public ICollection<InventoryDisposition> Dispositions { get; set; } = new List<InventoryDisposition>();
    public ICollection<ReturnDecisionProposalItem> DecisionProposals { get; set; } = new List<ReturnDecisionProposalItem>();
}
