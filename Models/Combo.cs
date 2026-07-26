using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public enum ComboPricingType
{
    SumOfItems,
    FixedPrice,
    PercentageDiscount,
    FixedDiscount
}

public enum ComboLifecycleStatus
{
    Draft,
    Scheduled,
    Active,
    Archived
}

public class Combo
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    // Kept for compatibility with existing catalog queries. Lifecycle status and
    // schedule are the authoritative availability rules for combos.
    public bool IsActive { get; set; } = true;

    public ComboLifecycleStatus Status { get; set; } = ComboLifecycleStatus.Active;

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public ComboPricingType PricingType { get; set; } = ComboPricingType.SumOfItems;

    public decimal? FixedPrice { get; set; }

    public decimal? DiscountValue { get; set; }

    public bool AllowCouponStacking { get; set; } = true;

    public int Revision { get; set; } = 1;

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
    public virtual ICollection<ComboAuditLog> AuditLogs { get; set; } = new List<ComboAuditLog>();

    public bool IsAvailableAt(DateTimeOffset now) =>
        IsActive &&
        Status is ComboLifecycleStatus.Active or ComboLifecycleStatus.Scheduled &&
        (!StartsAt.HasValue || StartsAt.Value <= now) &&
        (!EndsAt.HasValue || EndsAt.Value > now);

    public ComboLifecycleStatus GetEffectiveStatus(DateTimeOffset now)
    {
        if (!IsActive || Status == ComboLifecycleStatus.Archived) return ComboLifecycleStatus.Archived;
        if (Status == ComboLifecycleStatus.Draft) return ComboLifecycleStatus.Draft;
        if (StartsAt.HasValue && StartsAt.Value > now) return ComboLifecycleStatus.Scheduled;
        if (EndsAt.HasValue && EndsAt.Value <= now) return ComboLifecycleStatus.Archived;
        return ComboLifecycleStatus.Active;
    }
}
