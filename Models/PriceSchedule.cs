using System.ComponentModel.DataAnnotations;
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
    Cancelled,
    StoppedEarly
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
    public DateTimeOffset? CancelledAt { get; set; }
    public int? CancelledByAdminId { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    public int Revision { get; set; } = 1;
    public int? CreatedByAdminId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual Product Product { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual User? CreatedByAdmin { get; set; }
    public virtual User? CancelledByAdmin { get; set; }

    [NotMapped]
    public PriceScheduleStatus Status => GetStatus(DateTimeOffset.UtcNow);

    public PriceScheduleStatus GetStatus(DateTimeOffset now)
    {
        if (IsCancelled)
        {
            return CancelledAt.HasValue && CancelledAt.Value > StartsAt
                ? PriceScheduleStatus.StoppedEarly
                : PriceScheduleStatus.Cancelled;
        }

        if (now < StartsAt)
            return PriceScheduleStatus.Scheduled;

        if (EndsAt.HasValue && now >= EndsAt.Value)
            return PriceScheduleStatus.Ended;

        return PriceScheduleStatus.Active;
    }

    public bool IsActiveAt(DateTimeOffset instant) =>
        !IsCancelled && StartsAt <= instant && (!EndsAt.HasValue || instant < EndsAt.Value);
}
