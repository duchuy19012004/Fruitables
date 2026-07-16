using Fruitables.Models;

namespace Fruitables.ViewModels;

public sealed record PriceQuote(int ProductId, int? ProductVariantId, decimal BasePrice, decimal EffectivePrice, int? ScheduleId)
{
    public bool IsDiscounted => EffectivePrice < BasePrice;
    public decimal Savings => BasePrice - EffectivePrice;
}

public readonly record struct PriceTargetKey(int ProductId, int? ProductVariantId);

public class ProductPriceProjection
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsFeatured { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

public class SavePriceScheduleRequest
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}

public enum PriceAdjustmentType { Amount, Percentage }
public enum PriceAdjustmentDirection { Increase, Decrease }

public class BulkPriceUpdateRequest
{
    public List<PriceTargetKey> Targets { get; set; } = new();
    public PriceAdjustmentType AdjustmentType { get; set; }
    public PriceAdjustmentDirection Direction { get; set; }
    public decimal Value { get; set; }
}

public sealed record PriceManagementResult(bool Success, string? Error = null)
{
    public static PriceManagementResult Ok() => new(true);
    public static PriceManagementResult Fail(string error) => new(false, error);
}

public class PriceManagementRow
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? SKU { get; set; }
    public decimal BasePrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public int StockQuantity { get; set; }
    public PriceSchedule? CurrentSchedule { get; set; }
    public PriceSchedule? UpcomingSchedule { get; set; }
}

public class PriceManagementViewModel
{
    public string? Search { get; set; }
    public string? Filter { get; set; }
    public List<PriceManagementRow> Rows { get; set; } = new();
    public List<PriceSchedule> Schedules { get; set; } = new();
}
