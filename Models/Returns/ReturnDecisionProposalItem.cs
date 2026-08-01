using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models.Returns;

public class ReturnDecisionProposalItem
{
    public int Id { get; set; }
    public int ReturnDecisionProposalId { get; set; }
    public int ReturnRequestItemId { get; set; }
    public int ApprovedQuantity { get; set; }
    public ReturnDamagePercentage ApprovedDamagePercentage { get; set; }
    public ReturnCauseCode Cause { get; set; }
    public ReturnCostBearer CostBearer { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal ApprovedAmount { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }

    public ReturnDecisionProposal Proposal { get; set; } = null!;
    public ReturnRequestItem ReturnRequestItem { get; set; } = null!;
}
