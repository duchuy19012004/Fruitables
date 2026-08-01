using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models.Returns;

public class ReturnAccountSupportRestriction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ReturnAccountSupportLevel Level { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
