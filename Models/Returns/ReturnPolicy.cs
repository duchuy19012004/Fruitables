using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models.Returns;

public class ReturnPolicy
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public ReturnPolicyScope Scope { get; set; }
    public int? CategoryId { get; set; }
    public int? ProductId { get; set; }
    public ReturnReasonCode Reason { get; set; }
    public int ClaimWindowHours { get; set; }
    public bool EvidenceRequired { get; set; }
    public bool AllowPartialRefund { get; set; }
    public bool AllowFullRefund { get; set; }
    public bool AllowReplacement { get; set; }
    public bool AllowStoreCredit { get; set; }
    public bool AllowRestock { get; set; }
    public bool IsEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public ReturnDamagePercentageOptions AllowedDamagePercentages { get; set; } = ReturnDamagePercentageOptions.All;
    public bool AutoApprovalEnabled { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal AutoApprovalAmountCap { get; set; } = 100_000m;
    [Column(TypeName = "decimal(5,2)")] public decimal AutoApprovalOrderRatioCap { get; set; } = 30m;
    [Column(TypeName = "decimal(5,2)")] public decimal PostReviewSampleRate { get; set; } = 10m;
    public int SupplementWindowHours { get; set; } = 24;
    public int AppealWindowHours { get; set; } = 24;
    public int Version { get; set; } = 1;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }

    public Category? Category { get; set; }
    public Product? Product { get; set; }
}
