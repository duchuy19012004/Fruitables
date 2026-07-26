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
    public int ExpectedRevision { get; set; }
}

public class CancelPriceScheduleRequest
{
    public int ExpectedRevision { get; set; }
    public string? Reason { get; set; }
}

public enum PriceAdjustmentType { Amount, Percentage }
public enum PriceAdjustmentDirection { Increase, Decrease }

public class UpdateBasePriceRequest
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public decimal NewPrice { get; set; }
    public decimal ExpectedBasePrice { get; set; }
    public int ExpectedRevision { get; set; }

    public PriceTargetKey Target => new(ProductId, ProductVariantId);
}

public class BulkPriceTargetRequest
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public decimal ExpectedBasePrice { get; set; }
    public int ExpectedRevision { get; set; }

    public PriceTargetKey Target => new(ProductId, ProductVariantId);
}

public class BulkPriceUpdateRequest
{
    public List<BulkPriceTargetRequest> Targets { get; set; } = new();
    public PriceAdjustmentType AdjustmentType { get; set; }
    public PriceAdjustmentDirection Direction { get; set; }
    public decimal Value { get; set; }
}

public sealed record PriceManagementResult(bool Success, string? Error = null, int? Revision = null)
{
    public static PriceManagementResult Ok(int? revision = null) => new(true, null, revision);
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
    public int PriceRevision { get; set; }
    public PriceSchedule? CurrentSchedule { get; set; }
    public PriceSchedule? UpcomingSchedule { get; set; }
    public List<PriceSchedule> Schedules { get; set; } = new();
}

/// <summary>Tham số truy vấn dashboard giá (2 tab: bảng giá / lịch giảm giá)</summary>
public class PriceDashboardQuery
{
    public string Tab { get; set; } = "prices";          // prices | schedules
    public string? Search { get; set; }
    public string? Filter { get; set; }                  // null | active | upcoming | regular
    public string? Sort { get; set; }                    // name | base | effective
    public string? Dir { get; set; }                     // asc | desc
    public int Page { get; set; } = 1;                   // tab 1: trang theo NHÓM sản phẩm
    public int PageSize { get; set; } = 20;
    public string? ScheduleStatus { get; set; }          // null | active | scheduled | ended | cancelled
    public string? ScheduleSearch { get; set; }
    public int SchedulePage { get; set; } = 1;
    public int SchedulePageSize { get; set; } = 20;
}

/// <summary>Một mục trong combobox chọn đối tượng của modal tạo/sửa lịch</summary>
public class ScheduleTargetItem
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string? SKU { get; set; }
    public decimal BasePrice { get; set; }
    public int PriceRevision { get; set; }
}

public class PriceManagementViewModel
{
    public string? Search { get; set; }
    public string? Filter { get; set; }
    public List<PriceManagementRow> Rows { get; set; } = new();
    public int StatTotal { get; set; }
    public int StatActive { get; set; }
    public int StatUpcoming { get; set; }
    public int StatRegular { get; set; }

    // Tab & phân trang tab Bảng giá (theo nhóm sản phẩm)
    public string Tab { get; set; } = "prices";
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalGroups { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalGroups / (double)PageSize);

    // Dữ liệu combobox đối tượng lịch (mọi sản phẩm/biến thể, không phụ thuộc trang)
    public List<ScheduleTargetItem> ScheduleTargets { get; set; } = new();

    // Tab Lịch giảm giá
    public string? ScheduleStatus { get; set; }
    public string? ScheduleSearch { get; set; }
    public PagedResult<PriceSchedule> SchedulesPage { get; set; } = new();
    public Dictionary<string, int> ScheduleStatusCounts { get; set; } = new();
}
