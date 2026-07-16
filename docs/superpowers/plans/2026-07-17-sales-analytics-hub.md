# Sales Analytics Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace separate Admin Revenue + Cancelled Orders pages with one merchandising-focused Sales Analytics Hub (Gross/Net metrics, three tabs, chart-dense Classic UI, Excel export, legacy redirects).

**Architecture:** Introduce pure `SalesMetricEngine` + `ISalesAnalyticsService` facade over `IUnitOfWork` order queries; SSR hub via new `AnalyticsController`; Chart.js client for inventory in design §6; deprecate old revenue/cancel statistics services after cutover.

**Tech Stack:** ASP.NET Core 8 MVC (Areas/Admin), EF Core, xUnit + SQLite in-memory tests (`TestDbContextFactory`), Chart.js (CDN or existing), ClosedXML, Font Awesome, Be Vietnam Pro admin layout.

**Spec:** `docs/superpowers/specs/2026-07-17-sales-analytics-hub-design.md`  
**Prototype:** `docs/superpowers/prototypes/sales-analytics-hub/index.html` (Variant A visual)

## Global Constraints

- Target framework: **net8.0**
- Metrics: **Gross = Paid**; **Net = Delivered+Paid − Refund**; time basis **`Order.CreatedAt`** (Vietnam store convention)
- Default filter: **Last30Days** vs **previous contiguous 30 days**
- Custom range max **366 days**; merch table default top **50**, max **200**
- SQLite: **no `SumAsync` on decimal** — materialize then `Sum()` in memory
- UI: Admin Fruitables green `#81c408`, no emoji (Font Awesome icons), toast not `alert`
- Do not implement Giá & KM tab or recognition-by-delivery-date in v1
- Follow existing `UnitOfWork` / `AsNoTracking` patterns; register DI in `Program.cs`
- Tests live in `Tests/` project; run with `dotnet test Tests/Fruitables.Tests.csproj --filter <name>`

## File map

| Path | Responsibility |
|------|----------------|
| `ViewModels/SalesAnalyticsViewModels.cs` | Hub VMs, `MetricValue`, ranges, chart series DTOs |
| `Services/Analytics/SalesMetricEngine.cs` | Pure set predicates + money formulas |
| `Services/Analytics/AnalyticsPeriodHelper.cs` | `ToPeriodPair`, exclusive-end normalize, max-span validate |
| `Services/Interfaces/ISalesAnalyticsService.cs` | Facade contract |
| `Services/SalesAnalyticsService.cs` | Overview / merch / cancellations / export data |
| `Areas/Admin/Controllers/AnalyticsController.cs` | Hub + export + optional trend grain JSON |
| `Areas/Admin/Controllers/RevenueController.cs` | Redirect-only (or thin redirects) |
| `Areas/Admin/Views/Analytics/Index.cshtml` | Hub shell + 3 tabs |
| `Areas/Admin/Views/Analytics/_FilterBar.cshtml` | Global filter |
| `Areas/Admin/Views/Analytics/_OverviewTab.cshtml` | KPIs + charts canvas + top tables |
| `Areas/Admin/Views/Analytics/_MerchTab.cshtml` | Ranking + charts |
| `Areas/Admin/Views/Analytics/_CancellationsTab.cshtml` | Cancel KPIs + charts |
| `wwwroot/js/sales-analytics.js` | Chart.js bootstrap from JSON embeds |
| `wwwroot/css/sales-analytics.css` | Hub layout (classic A) |
| `Areas/Admin/Views/Shared/_AdminSidebar.cshtml` | Single menu entry |
| `Program.cs` | DI registration |
| `Tests/SalesMetricEngineTests.cs` | Unit metric/date tests |
| `Tests/SalesAnalyticsServiceTests.cs` | Integration with SQLite |

**Retire (after cutover):** heavy logic in `RevenueStatisticsService` / `CancelledOrdersStatisticsService` — keep types only if tests still need, or delete and migrate `RevenueServiceDateBoundaryTests` / `CancelledOrdersStatisticsServiceTests` to new service.

---

### Task 1: Domain types + period pair helper

**Files:**
- Create: `ViewModels/SalesAnalyticsViewModels.cs`
- Create: `Services/Analytics/AnalyticsPeriodHelper.cs`
- Modify: `ViewModels/RevenueViewModels.cs` (only if extending `DateRangePresetExtensions`; prefer new helper that *calls* existing `ToDateRange`)
- Test: `Tests/AnalyticsPeriodHelperTests.cs`

**Interfaces:**
- Consumes: `DateRangePreset`, `DateRangePresetExtensions.ToDateRange`, `GetVietnamToday`
- Produces: `AnalyticsDateRange`, `AnalyticsPeriodPair`, `MetricValue`, `AnalyticsPeriodHelper.ResolvePair(...)`, `MetricValue.From(current, previous)`

- [ ] **Step 1: Add view-model types**

Create `ViewModels/SalesAnalyticsViewModels.cs`:

```csharp
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
```

- [ ] **Step 2: Write failing period helper tests**

Create `Tests/AnalyticsPeriodHelperTests.cs`:

```csharp
using Fruitables.Services.Analytics;
using Fruitables.ViewModels;
using Xunit;

namespace Fruitables.Tests;

public class AnalyticsPeriodHelperTests
{
    [Fact]
    public void ResolvePair_Last30Days_PreviousIsContiguousEqualLength()
    {
        var today = new DateTime(2026, 7, 16);
        var pair = AnalyticsPeriodHelper.ResolvePair(DateRangePreset.Last30Days, null, null, today);

        Assert.Equal(new DateTime(2026, 6, 17), pair.Current.StartInclusive.Date);
        Assert.Equal(new DateTime(2026, 7, 17), pair.Current.EndExclusive.Date);

        var currentDays = (pair.Current.EndExclusive - pair.Current.StartInclusive).TotalDays;
        var prevDays = (pair.Previous.EndExclusive - pair.Previous.StartInclusive).TotalDays;
        Assert.Equal(currentDays, prevDays);
        Assert.Equal(pair.Current.StartInclusive, pair.Previous.EndExclusive);
    }

    [Fact]
    public void ResolvePair_CustomOver366Days_Throws()
    {
        var from = new DateTime(2025, 1, 1);
        var to = new DateTime(2026, 2, 2);
        Assert.Throws<ArgumentException>(() =>
            AnalyticsPeriodHelper.ResolvePair(DateRangePreset.Custom, from, to, new DateTime(2026, 7, 16)));
    }

    [Fact]
    public void MetricValue_From_PreviousZeroCurrentPositive_DeltaPercentNull()
    {
        var m = MetricValue.From(100, 0);
        Assert.Null(m.DeltaPercent);
        Assert.Equal(100, m.Delta);
    }
}
```

- [ ] **Step 3: Run tests — expect FAIL**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~AnalyticsPeriodHelperTests" -v q
```

Expected: compile fail or missing type `AnalyticsPeriodHelper`.

- [ ] **Step 4: Implement `AnalyticsPeriodHelper`**

Create `Services/Analytics/AnalyticsPeriodHelper.cs`:

```csharp
using Fruitables.ViewModels;

namespace Fruitables.Services.Analytics;

public static class AnalyticsPeriodHelper
{
    public const int MaxRangeDays = 366;

    /// <summary>
    /// Builds current+previous ranges. Inclusive calendar semantics from DateRangePreset
    /// are converted to [start, endExclusive).
    /// </summary>
    public static AnalyticsPeriodPair ResolvePair(
        DateRangePreset preset,
        DateTime? from,
        DateTime? to,
        DateTime? vietnamToday = null,
        DateTime? firstOrderDate = null)
    {
        var today = (vietnamToday ?? DateRangePresetExtensions.GetVietnamToday()).Date;
        DateTime start;
        DateTime endExclusive;

        if (preset == DateRangePreset.Custom)
        {
            if (!from.HasValue || !to.HasValue)
                throw new ArgumentException("Custom requires From and To.");
            if (from.Value.Date > to.Value.Date)
                throw new ArgumentException("From must be <= To.");
            start = from.Value.Date;
            endExclusive = to.Value.Date.AddDays(1);
        }
        else
        {
            // ToDateRange returns inclusive end-of-day; convert to exclusive next-day midnight
            var (s, e) = preset.ToDateRange(firstOrderDate);
            // Recompute relative to fixed `today` when tests inject vietnamToday:
            // Prefer calling ToDateRange only when vietnamToday is null; for tests, reimplement Last30Days etc. via today.
            (s, e) = ResolvePresetAgainstToday(preset, today, firstOrderDate);
            start = s.Date;
            endExclusive = e.Date == e ? e.Date.AddDays(1) : e.AddTicks(1); // safe: prefer
            // Simpler exclusive rule:
            endExclusive = s.Date == e.Date
                ? s.Date.AddDays(1)
                : e.Date.AddDays(e.TimeOfDay == TimeSpan.Zero ? 0 : 1);
            // CLEAN implementation for implementer:
            // After ToDateRange(start, endInclusiveEndOfDay): endExclusive = end.Date.AddDays(1) if time is end-of-day.
        }

        // Implementer: final clean version:
        // start = rangeStart.Date; endExclusive = rangeEnd.Date.AddDays(1) when ToDateRange end is end-of-day on last day.

        var days = (endExclusive - start).TotalDays;
        if (days > MaxRangeDays)
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");
        if (days <= 0)
            throw new ArgumentException("Khoảng thời gian không hợp lệ.");

        var prevEnd = start;
        var prevStart = start.AddDays(-days);
        var currentLabel = $"{start:dd/MM/yyyy} – {endExclusive.AddDays(-1):dd/MM/yyyy}";
        var prevLabel = $"{prevStart:dd/MM/yyyy} – {prevEnd.AddDays(-1):dd/MM/yyyy}";

        return new AnalyticsPeriodPair(
            new AnalyticsDateRange(start, endExclusive, currentLabel),
            new AnalyticsDateRange(prevStart, prevEnd, prevLabel));
    }

    private static (DateTime Start, DateTime End) ResolvePresetAgainstToday(
        DateRangePreset preset, DateTime today, DateTime? firstOrderDate)
    {
        return preset switch
        {
            DateRangePreset.Today => (today, today.AddDays(1).AddTicks(-1)),
            DateRangePreset.Yesterday => (today.AddDays(-1), today.AddTicks(-1)),
            DateRangePreset.Last7Days => (today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.Last30Days => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.LastMonth => DateRangePresetExtensions.GetLastMonthRange(today),
            DateRangePreset.LastWeek => DateRangePresetExtensions.GetLastWeekRange(today),
            DateRangePreset.ThisYear => (new DateTime(today.Year, 1, 1), today.AddDays(1).AddTicks(-1)),
            DateRangePreset.AllTime => (firstOrderDate?.Date ?? DateTime.MinValue.Date, today.AddDays(1).AddTicks(-1)),
            _ => throw new ArgumentException($"Unknown preset: {preset}")
        };
    }

    public static bool InRange(DateTime createdAt, AnalyticsDateRange range) =>
        createdAt >= range.StartInclusive && createdAt < range.EndExclusive;
}
```

**Implementer note:** Finish exclusive-end conversion cleanly as:

```csharp
var (s, eInclusive) = ResolvePresetAgainstToday(...);
var start = s.Date;
var endExclusive = eInclusive.Date.AddDays(1); // eInclusive is always end-of-last-day from presets
```

For `Last30Days` with today=2026-07-16: start=2026-06-17, endExclusive=2026-07-17.

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~AnalyticsPeriodHelperTests" -v q
```

- [ ] **Step 6: Commit**

```bash
git add ViewModels/SalesAnalyticsViewModels.cs Services/Analytics/AnalyticsPeriodHelper.cs Tests/AnalyticsPeriodHelperTests.cs
git commit -m "feat(analytics): period pair helper and sales analytics VMs"
```

---

### Task 2: SalesMetricEngine (TDD)

**Files:**
- Create: `Services/Analytics/SalesMetricEngine.cs`
- Create: `Services/Analytics/OrderAnalyticsSnapshot.cs` (minimal DTO for pure functions)
- Test: `Tests/SalesMetricEngineTests.cs`

**Interfaces:**
- Consumes: `OrderStatus`, `PaymentStatus`, order money fields
- Produces: `SalesMetricEngine.IsPaid/IsDelivered/IsRefund/IsCancelled`, `Gross`, `Net`, `CancelRate`, `RefundRate`, `Aov`

- [ ] **Step 1: Write failing metric tests**

```csharp
using Fruitables.Models;
using Fruitables.Services.Analytics;
using Xunit;

namespace Fruitables.Tests;

public class SalesMetricEngineTests
{
    private static OrderSnap O(decimal total, PaymentStatus pay, OrderStatus st) =>
        new(total, pay, st, 0, 0, total);

    [Fact]
    public void Gross_SumsPaidOnly()
    {
        var orders = new[]
        {
            O(100, PaymentStatus.Paid, OrderStatus.Delivered),
            O(50, PaymentStatus.Paid, OrderStatus.Processing),
            O(80, PaymentStatus.Pending, OrderStatus.Pending),
            O(20, PaymentStatus.Refunded, OrderStatus.Returned),
        };
        Assert.Equal(150, SalesMetricEngine.Gross(orders));
    }

    [Fact]
    public void Net_DeliveredMinusRefund()
    {
        var orders = new[]
        {
            O(100, PaymentStatus.Paid, OrderStatus.Delivered),
            O(40, PaymentStatus.Paid, OrderStatus.Delivered),
            O(30, PaymentStatus.Refunded, OrderStatus.Returned),
            O(50, PaymentStatus.Paid, OrderStatus.Processing),
        };
        Assert.Equal(110, SalesMetricEngine.Net(orders)); // 140 - 30
    }

    [Fact]
    public void CancelRate_UsesAllOrdersDenominator()
    {
        var orders = new[]
        {
            O(1, PaymentStatus.Paid, OrderStatus.Delivered),
            O(1, PaymentStatus.Pending, OrderStatus.Cancelled),
            O(1, PaymentStatus.Pending, OrderStatus.Cancelled),
            O(1, PaymentStatus.Paid, OrderStatus.Processing),
        };
        Assert.Equal(50m, SalesMetricEngine.CancelRatePercent(orders));
    }

    [Fact]
    public void RefundRate_PaidDenominator()
    {
        var orders = new[]
        {
            O(1, PaymentStatus.Paid, OrderStatus.Delivered),
            O(1, PaymentStatus.Paid, OrderStatus.Delivered),
            O(1, PaymentStatus.Refunded, OrderStatus.Returned),
        };
        // refund count / paid count = 1/2 = 50
        Assert.Equal(50m, SalesMetricEngine.RefundRatePercent(orders));
    }
}
```

- [ ] **Step 2: Run — FAIL**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SalesMetricEngineTests" -v q
```

- [ ] **Step 3: Implement engine**

```csharp
// Services/Analytics/OrderAnalyticsSnapshot.cs
namespace Fruitables.Services.Analytics;
using Fruitables.Models;

public readonly record struct OrderAnalyticsSnapshot(
    decimal Total,
    PaymentStatus PaymentStatus,
    OrderStatus Status,
    decimal Discount,
    decimal ShippingFee,
    decimal Subtotal);

// Services/Analytics/SalesMetricEngine.cs
namespace Fruitables.Services.Analytics;
using Fruitables.Models;

public static class SalesMetricEngine
{
    public static bool IsPaid(OrderAnalyticsSnapshot o) => o.PaymentStatus == PaymentStatus.Paid;
    public static bool IsDelivered(OrderAnalyticsSnapshot o) =>
        o.PaymentStatus == PaymentStatus.Paid && o.Status == OrderStatus.Delivered;
    public static bool IsRefund(OrderAnalyticsSnapshot o) => o.PaymentStatus == PaymentStatus.Refunded;
    public static bool IsCancelled(OrderAnalyticsSnapshot o) => o.Status == OrderStatus.Cancelled;

    public static decimal Gross(IEnumerable<OrderAnalyticsSnapshot> orders) =>
        orders.Where(IsPaid).Sum(o => o.Total);

    public static decimal Net(IEnumerable<OrderAnalyticsSnapshot> orders) =>
        orders.Where(IsDelivered).Sum(o => o.Total) - orders.Where(IsRefund).Sum(o => o.Total);

    public static int CountPaid(IEnumerable<OrderAnalyticsSnapshot> orders) => orders.Count(IsPaid);
    public static int CountDelivered(IEnumerable<OrderAnalyticsSnapshot> orders) => orders.Count(IsDelivered);
    public static int CountCancelled(IEnumerable<OrderAnalyticsSnapshot> orders) => orders.Count(IsCancelled);
    public static int CountRefund(IEnumerable<OrderAnalyticsSnapshot> orders) => orders.Count(IsRefund);

    public static decimal CancelRatePercent(IEnumerable<OrderAnalyticsSnapshot> orders)
    {
        var list = orders as IList<OrderAnalyticsSnapshot> ?? orders.ToList();
        if (list.Count == 0) return 0;
        return Math.Round((decimal)CountCancelled(list) / list.Count * 100m, 2);
    }

    public static decimal RefundRatePercent(IEnumerable<OrderAnalyticsSnapshot> orders)
    {
        var list = orders as IList<OrderAnalyticsSnapshot> ?? orders.ToList();
        var paid = CountPaid(list);
        if (paid == 0) return 0;
        return Math.Round((decimal)CountRefund(list) / paid * 100m, 2);
    }

    public static decimal Aov(decimal revenue, int orderCount) =>
        orderCount == 0 ? 0 : Math.Round(revenue / orderCount, 2);
}
```

- [ ] **Step 4: Run — PASS**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SalesMetricEngineTests" -v q
```

- [ ] **Step 5: Commit**

```bash
git add Services/Analytics/ Tests/SalesMetricEngineTests.cs
git commit -m "feat(analytics): SalesMetricEngine Gross/Net/cancel rules"
```

---

### Task 3: ISalesAnalyticsService + overview query

**Files:**
- Create: `Services/Interfaces/ISalesAnalyticsService.cs`
- Create: `Services/SalesAnalyticsService.cs`
- Modify: `Program.cs` (register scoped service)
- Test: `Tests/SalesAnalyticsServiceTests.cs`

**Interfaces:**
- Consumes: `IUnitOfWork`, `SalesMetricEngine`, `AnalyticsPeriodHelper`
- Produces: `Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter)`, trend grain optional later

- [ ] **Step 1: Write integration test for overview Gross/Net**

```csharp
// Tests/SalesAnalyticsServiceTests.cs — use TestDbContextFactory + UnitOfWork like other service tests
[Fact]
public async Task GetHubAsync_Overview_ComputesGrossAndNetForPeriod()
{
    var options = TestDbContextFactory.CreateSqliteOptions();
    await using var ctx = new ApplicationDbContext(options);
    // Seed: one Delivered+Paid 100 on 2026-07-01, one Paid+Processing 50, one Refunded 20, one Cancelled 10
    // Use CreatedAt inside Last30Days relative to fixed clock — inject today via filter Custom:
    var filter = new SalesAnalyticsFilterVm
    {
        Preset = DateRangePreset.Custom,
        From = new DateTime(2026, 7, 1),
        To = new DateTime(2026, 7, 16),
        Tab = SalesAnalyticsTab.Overview
    };
    // ... seed CreatedAt = 2026-07-05 for all ...
    var uow = new UnitOfWork(ctx);
    var sut = new SalesAnalyticsService(uow);
    var hub = await sut.GetHubAsync(filter);
    Assert.Null(hub.Error);
    Assert.NotNull(hub.Overview);
    Assert.Equal(150, hub.Overview!.Gross.Value); // 100+50 paid
    Assert.Equal(80, hub.Overview.Net.Value);     // 100 - 20
}
```

Mirror `PriceManagementServiceTests` / revenue tests for `UnitOfWork` construction.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement service overview**

```csharp
public interface ISalesAnalyticsService
{
    Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter);
    Task<byte[]> ExportExcelAsync(SalesAnalyticsFilterVm filter);
}

public class SalesAnalyticsService : ISalesAnalyticsService
{
    private readonly IUnitOfWork _uow;
    public SalesAnalyticsService(IUnitOfWork uow) => _uow = uow;

    public async Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter)
    {
        try
        {
            var firstOrder = await _uow.Orders.Query().AsNoTracking()
                .OrderBy(o => o.CreatedAt).Select(o => (DateTime?)o.CreatedAt).FirstOrDefaultAsync();
            var pair = AnalyticsPeriodHelper.ResolvePair(filter.Preset, filter.From, filter.To, firstOrderDate: firstOrder);
            filter.Take = Math.Clamp(filter.Take <= 0 ? 50 : filter.Take, 1, 200);

            var hub = new SalesHubVm { Filter = filter, Periods = pair };

            // Load order snapshots for current+previous in one query spanning prevStart..currentEnd
            var min = pair.Previous.StartInclusive;
            var max = pair.Current.EndExclusive;
            var rows = await _uow.Orders.Query().AsNoTracking()
                .Where(o => o.CreatedAt >= min && o.CreatedAt < max)
                .Select(o => new
                {
                    o.Id, o.CreatedAt, o.Total, o.Discount, o.ShippingFee, o.Subtotal,
                    o.PaymentStatus, o.Status, o.CancelReason
                }).ToListAsync();

            var cur = rows.Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Current))
                .Select(o => new OrderAnalyticsSnapshot(o.Total, o.PaymentStatus, o.Status, o.Discount, o.ShippingFee, o.Subtotal)).ToList();
            var prev = rows.Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Previous))
                .Select(o => new OrderAnalyticsSnapshot(o.Total, o.PaymentStatus, o.Status, o.Discount, o.ShippingFee, o.Subtotal)).ToList();

            if (filter.Tab == SalesAnalyticsTab.Overview || true /* always fill active tab only */)
            {
                // Fill only filter.Tab to save work:
            }

            switch (filter.Tab)
            {
                case SalesAnalyticsTab.Overview:
                    hub.Overview = await BuildOverviewAsync(pair, cur, prev, rows);
                    break;
                case SalesAnalyticsTab.Merch:
                    hub.Merch = await BuildMerchAsync(pair, filter);
                    break;
                case SalesAnalyticsTab.Cancellations:
                    hub.Cancellations = BuildCancellations(pair, cur, prev, rows);
                    break;
            }
            return hub;
        }
        catch (ArgumentException ex)
        {
            return new SalesHubVm { Filter = filter, Error = ex.Message };
        }
    }
    // BuildOverviewAsync: MetricValue.From for each KPI; build ChartSeriesDto from daily buckets
    // Use Vietnam date grouping on CreatedAt.Date
}
```

Implement `BuildOverviewAsync` fully: daily labels between current start and endExclusive-1 day; Gross/Net per day; paid/cancel counts; category mix via join items (next task can stub empty series then fill).

For overview top products, call shared merch rank helper with take=5.

- [ ] **Step 4: Register DI**

In `Program.cs` next to revenue services:

```csharp
builder.Services.AddScoped<ISalesAnalyticsService, SalesAnalyticsService>();
```

- [ ] **Step 5: Tests PASS + commit**

```bash
dotnet test Tests/Fruitables.Tests.csproj --filter "FullyQualifiedName~SalesAnalyticsServiceTests" -v q
git add Services/ Program.cs Tests/SalesAnalyticsServiceTests.cs ViewModels/SalesAnalyticsViewModels.cs
git commit -m "feat(analytics): SalesAnalyticsService overview Gross/Net"
```

---

### Task 4: Merch ranking + cancellation aggregates

**Files:**
- Modify: `Services/SalesAnalyticsService.cs`
- Modify: `Tests/SalesAnalyticsServiceTests.cs`

**Interfaces:**
- Produces: `BuildMerchAsync`, `BuildCancellations` with charts DTOs populated

- [ ] **Step 1: Test merch ranking**

Seed category + product + order + order items (Delivered). Assert top product Net = line total, share 100% when single product.

- [ ] **Step 2: Implement merch**

```csharp
// Pseudo:
// deliveredOrderIds in current period
// join OrderItems where OrderId in set
// group by ProductId: Sum(Price*Qty), Sum(Qty), Count distinct OrderId
// join Product.Name, Category.Name
// previous period same for DeltaPercent
// sort by Net desc, take N
```

Use `OrderItem.Price` and `Quantity` as stored on lines.

- [ ] **Step 3: Test + implement cancellations**

- Cancel metrics from engine  
- Reasons: group `CancelReason` null-coalesce `"Không ghi rõ"`  
- Value by product: items on cancelled orders  
- Charts series as labels/data lists  

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(analytics): merch ranking and cancellation aggregates"
```

---

### Task 5: AnalyticsController + legacy redirects

**Files:**
- Create: `Areas/Admin/Controllers/AnalyticsController.cs`
- Modify: `Areas/Admin/Controllers/RevenueController.cs` (redirect Index + CancelledOrders; keep Export temporarily redirecting or remove)

**Interfaces:**
- Consumes: `ISalesAnalyticsService`
- Produces: MVC actions `Index`, `Export`

- [ ] **Step 1: Controller**

```csharp
[Area("Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AnalyticsController : Controller
{
    private readonly ISalesAnalyticsService _analytics;
    public AnalyticsController(ISalesAnalyticsService analytics) => _analytics = analytics;

    [HttpGet]
    public async Task<IActionResult> Index(
        DateRangePreset preset = DateRangePreset.Last30Days,
        DateTime? from = null,
        DateTime? to = null,
        string tab = "overview",
        string dimension = "product",
        string? sort = null,
        string? dir = null,
        int take = 50)
    {
        var filter = new SalesAnalyticsFilterVm
        {
            Preset = preset,
            From = from,
            To = to,
            Tab = tab?.ToLowerInvariant() switch
            {
                "merch" => SalesAnalyticsTab.Merch,
                "cancellations" or "cancel" => SalesAnalyticsTab.Cancellations,
                _ => SalesAnalyticsTab.Overview
            },
            Dimension = dimension?.ToLowerInvariant() == "category"
                ? MerchDimension.Category : MerchDimension.Product,
            Sort = sort,
            Dir = dir,
            Take = take
        };
        var vm = await _analytics.GetHubAsync(filter);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Export(/* same query params as Index */)
    {
        // build filter, call ExportExcelAsync, return File(...)
    }
}
```

- [ ] **Step 2: RevenueController redirects**

```csharp
[HttpGet]
public IActionResult Index() =>
    RedirectToAction("Index", "Analytics", new { area = "Admin", tab = "overview" });

[HttpGet]
public IActionResult CancelledOrders() =>
    RedirectToAction("Index", "Analytics", new { area = "Admin", tab = "cancellations" });
```

Remove or stub old POST/GET data actions that would 404 if bookmarked — redirect ExportReport to Analytics Export if needed.

- [ ] **Step 3: Manual smoke** — run app, hit `/Admin/Revenue` → expect redirect (after views exist Task 6).

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(analytics): AnalyticsController and legacy Revenue redirects"
```

---

### Task 6: Hub views + CSS (Classic A)

**Files:**
- Create: `Areas/Admin/Views/Analytics/Index.cshtml`
- Create: `Areas/Admin/Views/Analytics/_FilterBar.cshtml`
- Create: `Areas/Admin/Views/Analytics/_OverviewTab.cshtml`
- Create: `Areas/Admin/Views/Analytics/_MerchTab.cshtml`
- Create: `Areas/Admin/Views/Analytics/_CancellationsTab.cshtml`
- Create: `wwwroot/css/sales-analytics.css`

**Interfaces:**
- Consumes: `SalesHubVm`
- Layout: `_AdminDashboardLayout.cshtml`

- [ ] **Step 1: CSS** — port structure from prototype Variant A: `.sa-page`, filter sticky, tabs, kpi grid 6, chart cards, tables. Accent `#81c408`.

- [ ] **Step 2: Index.cshtml**

```cshtml
@model Fruitables.ViewModels.SalesHubVm
@{
    ViewData["Title"] = "Thống kê bán hàng";
    Layout = "~/Areas/Admin/Views/Shared/_AdminDashboardLayout.cshtml";
}
@section Styles {
    <link rel="stylesheet" href="~/css/sales-analytics.css" />
}
<div class="sa-page container-fluid py-4">
  <!-- header + export link with query -->
  @if (!string.IsNullOrEmpty(Model.Error)) {
    <div class="alert alert-danger">@Model.Error</div>
  }
  @await Html.PartialAsync("_FilterBar", Model)
  <div class="sa-tabs">...</div>
  @switch (Model.Filter.Tab) {
    case SalesAnalyticsTab.Overview:
      @await Html.PartialAsync("_OverviewTab", Model); break;
    case SalesAnalyticsTab.Merch:
      @await Html.PartialAsync("_MerchTab", Model); break;
    default:
      @await Html.PartialAsync("_CancellationsTab", Model); break;
  }
</div>
@section Scripts {
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <script src="~/js/sales-analytics.js"></script>
  <script>
    window.saCharts = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(/* chart payload from active tab */));
    document.addEventListener('DOMContentLoaded', () => SalesAnalytics.init(window.saCharts));
  </script>
}
```

Embed chart JSON from the active tab VM (serialize `ChartSeriesDto` objects).

- [ ] **Step 3: Filter bar** — GET form to `Analytics/Index`; presets as links or submit; show compare chip from `Model.Periods.Previous.Label`.

- [ ] **Step 4: KPI partial helper** — display `MetricValue` with ↑↓ and `N0` đ formatting.

- [ ] **Step 5: Visual check** against prototype A (sidebar, density, dual Gross/Net colors).

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(analytics): Classic hub Razor views and CSS"
```

---

### Task 7: Chart.js client (`sales-analytics.js`)

**Files:**
- Create: `wwwroot/js/sales-analytics.js`

**Interfaces:**
- Consumes: JSON `{ trend, ordersVolume, categoryMix, ... }` matching canvas `data-chart` ids
- Produces: Chart instances; destroy on re-init

- [ ] **Step 1: Implement `SalesAnalytics.init(payload)`**  
  Map each key to canvas selector; reuse colors from prototype (gross slate, net green, cancel red).  
  Support: line dual, bar grouped, doughnut, horizontal bar, dual-axis, growth colors, cancel combo.

- [ ] **Step 2: Wire canvas elements** in each tab partial with stable ids: `sa-chart-trend`, etc.

- [ ] **Step 3: Manual** — load overview; confirm all overview charts render without console errors.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(analytics): Chart.js inventory for sales hub tabs"
```

---

### Task 8: Sidebar + export Excel

**Files:**
- Modify: `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`
- Modify: `Services/SalesAnalyticsService.cs` (`ExportExcelAsync`)
- Modify: `AnalyticsController.Export`

- [ ] **Step 1: Sidebar** — remove separate Doanh thu + Đơn hủy links; add:

```html
<a asp-action="Index" asp-controller="Analytics" asp-area="Admin"
   class="sidebar-nav-link @(controller == "Analytics" || controller == "Revenue" ? "active" : "")">
  <i class="fas fa-chart-line"></i>
  <span>Thống kê bán hàng</span>
</a>
```

- [ ] **Step 2: Export** — ClosedXML like `RevenueController.CreateOverviewSheet` but:
  - Sheet 1: metric definitions (Gross/Net text from spec)
  - Sheet 2: KPIs current vs previous
  - Sheet 3: active tab table (merch rows or cancel reasons)

- [ ] **Step 3: Manual export** download opens in Excel.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(analytics): sidebar entry and Excel export"
```

---

### Task 9: Cut over tests + remove dead code

**Files:**
- Migrate: `Tests/RevenueServiceDateBoundaryTests.cs` → assert new service period boundaries (or delete obsolete ConvertToUtc cases if engine uses exclusive CreatedAt compare only)
- Migrate/delete: `Tests/CancelledOrdersStatisticsServiceTests.cs`
- Optionally delete or thin `RevenueStatisticsService.cs` / `CancelledOrdersStatisticsService.cs` and unregister DI if no remaining references
- Grep for `IRevenueStatisticsService` / `ICancelledOrdersStatisticsService` and fix

- [ ] **Step 1:**

```bash
rg "IRevenueStatisticsService|ICancelledOrdersStatisticsService|RevenueStatisticsService|CancelledOrdersStatisticsService" --glob "*.cs"
```

- [ ] **Step 2:** Remove DI registrations when unused; delete obsolete services/tests or keep obsolete wrappers that forward to `ISalesAnalyticsService` for one release (prefer delete if zero refs).

- [ ] **Step 3:**

```bash
dotnet test Tests/Fruitables.Tests.csproj -v q
```

Expected: all green.

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(analytics): remove legacy revenue/cancel stats services"
```

---

### Task 10: End-to-end QA checklist

- [ ] **Step 1: Manual checklist**

1. Login admin → menu shows single **Thống kê bán hàng**.  
2. Default Last30Days overview: 6 KPIs, charts visible, no JS errors.  
3. Switch tabs merch/cancellations; filter preserved in query string.  
4. Custom invalid range (from &gt; to) shows error toast/alert-danger.  
5. `/Admin/Revenue` redirects to hub overview.  
6. `/Admin/Revenue/CancelledOrders` redirects to cancellations tab.  
7. Export returns `.xlsx`.  
8. Seed empty period → empty states, not crash.  
9. Compare chip shows previous label.  

- [ ] **Step 2: Final commit** if only doc/status tweaks; else done.

- [ ] **Step 3: Update spec status** line to `Accepted / Implemented` when complete (optional).

---

## Plan self-review (vs spec)

| Spec requirement | Task |
|------------------|------|
| Gross/Net definitions | Task 2–3 |
| Last30 vs previous 30 | Task 1 |
| Hub 3 tabs Classic A | Task 6–7 |
| Merch ranking + charts | Task 4, 7 |
| Cancellations tab | Task 4, 7 |
| Chart inventory §6 | Task 7 |
| Sidebar + redirects | Task 5, 8 |
| Excel export | Task 8 |
| Metric unit tests | Task 2 |
| Integration tests | Task 3–4, 9 |
| No Giá & KM / recognition date | Global constraints |
| SQLite decimal sum | Task 3 note |
| Max 366 days / take 200 | Task 1, 3 |

**Placeholder scan:** Period helper exclusive-end conversion is specified with a clean final rule; implementer must not leave the messy intermediate comments in production code.

**Type consistency:** `SalesHubVm`, `MetricValue`, `SalesAnalyticsTab`, `MerchDimension`, `ChartSeriesDto` used uniformly.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-17-sales-analytics-hub.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — this session executes tasks with checkpoints  

Which approach?
