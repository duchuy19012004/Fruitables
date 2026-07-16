namespace Fruitables.ViewModels;

public readonly record struct AnalyticsDateRange(
    DateTime StartInclusive,
    DateTime EndExclusive,
    string Label);

public readonly record struct AnalyticsPeriodPair(
    AnalyticsDateRange Current,
    AnalyticsDateRange Previous);

public sealed record MetricValue(
    decimal Value,
    decimal? Previous,
    decimal? Delta,
    decimal? DeltaPercent)
{
    public static MetricValue From(decimal current, decimal previous)
    {
        var delta = current - previous;
        decimal? pct = previous == 0
            ? (current == 0 ? 0 : null)
            : Math.Round(delta / previous * 100m, 2);
        return new MetricValue(current, previous, delta, pct);
    }
}

public enum SalesAnalyticsTab
{
    Overview,
    Merch,
    Cancellations
}

public enum MerchDimension
{
    Product,
    Category
}

public class SalesAnalyticsFilterVm
{
    public DateRangePreset Preset { get; set; } = DateRangePreset.Last30Days;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public SalesAnalyticsTab Tab { get; set; } = SalesAnalyticsTab.Overview;
    public MerchDimension Dimension { get; set; } = MerchDimension.Product;
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Take { get; set; } = 50;
}

// Chart series + tab VMs filled in later tasks; include stubs:

public class SalesHubVm
{
    public SalesAnalyticsFilterVm Filter { get; set; } = new();
    public AnalyticsPeriodPair Periods { get; set; }
    public string? Error { get; set; }
    public SalesOverviewVm? Overview { get; set; }
    public SalesMerchVm? Merch { get; set; }
    public SalesCancellationsVm? Cancellations { get; set; }
}

public class SalesOverviewVm
{
    public MetricValue Gross { get; set; } = MetricValue.From(0, 0);
    public MetricValue Net { get; set; } = MetricValue.From(0, 0);
    public MetricValue OrdersPaid { get; set; } = MetricValue.From(0, 0);
    public MetricValue AovNet { get; set; } = MetricValue.From(0, 0);
    public MetricValue CancelRate { get; set; } = MetricValue.From(0, 0);
    public ChartSeriesDto Trend { get; set; } = new();
    public ChartSeriesDto OrdersVolume { get; set; } = new();
    public ChartSeriesDto CategoryMix { get; set; } = new();
    public ChartSeriesDto AovTrend { get; set; } = new();
    public ChartSeriesDto UnitsTrend { get; set; } = new();
    public ChartSeriesDto Pipeline { get; set; } = new();
    public ChartSeriesDto PeriodCompare { get; set; } = new();
    public ChartSeriesDto TopProductsBar { get; set; } = new();
    public List<MerchRankRowVm> TopProducts { get; set; } = new();
    public List<MerchRankRowVm> TopCategories { get; set; } = new();
}

public class SalesMerchVm
{
    public MerchDimension Dimension { get; set; }
    public List<MerchRankRowVm> Rows { get; set; } = new();
    public ChartSeriesDto RankBar { get; set; } = new();
    public ChartSeriesDto CategoryMix { get; set; } = new();
    public ChartSeriesDto UnitsVsNet { get; set; } = new();
    public ChartSeriesDto Growth { get; set; } = new();
}

public class SalesCancellationsVm
{
    public MetricValue CancelledCount { get; set; } = MetricValue.From(0, 0);
    public MetricValue CancelRate { get; set; } = MetricValue.From(0, 0);
    public MetricValue CancelledValue { get; set; } = MetricValue.From(0, 0);
    public MetricValue RefundRate { get; set; } = MetricValue.From(0, 0);
    public ChartSeriesDto CancelTrend { get; set; } = new();
    public ChartSeriesDto Reasons { get; set; } = new();
    public ChartSeriesDto ValueByProduct { get; set; } = new();
    public ChartSeriesDto ValueByCategory { get; set; } = new();
}

public class MerchRankRowVm
{
    public int Rank { get; set; }
    public int? ProductId { get; set; }
    public int? CategoryId { get; set; }
    public string Name { get; set; } = "";
    public string? CategoryName { get; set; }
    public int Units { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal SharePercent { get; set; }
    public int OrderCount { get; set; }
    public decimal? DeltaPercent { get; set; }
}

public class ChartSeriesDto
{
    public List<string> Labels { get; set; } = new();
    public List<ChartDatasetDto> Datasets { get; set; } = new();
}

public class ChartDatasetDto
{
    public string Label { get; set; } = "";
    public List<decimal> Data { get; set; } = new();
}
