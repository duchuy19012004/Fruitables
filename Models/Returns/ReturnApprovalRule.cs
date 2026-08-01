using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models.Returns;

public class ReturnApprovalRule
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string RoleName { get; set; } = string.Empty;
    public ReturnApprovalAction Action { get; set; }
    [Column(TypeName = "decimal(12,2)")] public decimal? ThresholdAmount { get; set; }
    public bool RequiresDifferentActor { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
}
