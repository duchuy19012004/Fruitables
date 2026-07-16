using System.ComponentModel.DataAnnotations.Schema;

namespace Fruitables.Models;

public enum DiscountType
{
    FixedPrice,
    Percentage
}

public enum PriceScheduleStatus
{
    Scheduled,
    Active,
    Ended,
    Cancelled
}

public class PriceSchedule
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public DiscountType DiscountType { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Value { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsCancelled { get; set; }
    public int? CreatedByAdminId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual User? CreatedByAdmin { get; set; }

    [NotMapped]
    public PriceScheduleStatus Status => GetStatus(DateTimeOffset.UtcNow);

    public PriceScheduleStatus GetStatus(DateTimeOffset now) => IsCancelled
        ? PriceScheduleStatus.Cancelled
        : now < StartsAt
            ? PriceScheduleStatus.Scheduled
            : EndsAt.HasValue && now >= EndsAt.Value
                ? PriceScheduleStatus.Ended
                : PriceScheduleStatus.Active;

    public bool IsActiveAt(DateTimeOffset instant) =>
        !IsCancelled && StartsAt <= instant && (!EndsAt.HasValue || instant < EndsAt.Value);
}
