using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fruitables.Models;

namespace Fruitables.Models.Returns;

public class ReturnRequestItem
{
    public int Id { get; set; }

    public int ReturnRequestId { get; set; }
    public int OrderItemId { get; set; }
    public ReturnItemDecisionStatus DecisionStatus { get; set; } = ReturnItemDecisionStatus.Pending;

    [Column(TypeName = "decimal(10,2)")]
    public decimal RequestedQuantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ApprovedQuantity { get; set; }

    public ReturnReasonCode Reason { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? DecisionReason { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal RequestedAmount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal ApprovedAmount { get; set; }

    public virtual ReturnRequest ReturnRequest { get; set; } = null!;
    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual ICollection<ReturnEvidence> Evidence { get; set; } = new List<ReturnEvidence>();
}
