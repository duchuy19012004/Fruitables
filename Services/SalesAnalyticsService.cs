using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Analytics;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services;

public class SalesAnalyticsService : ISalesAnalyticsService
{
    private readonly IUnitOfWork _uow;

    public SalesAnalyticsService(IUnitOfWork uow) => _uow = uow;

    public async Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter)
    {
        try
        {
            var firstOrder = await _uow.Orders.Query().AsNoTracking()
                .OrderBy(o => o.CreatedAt)
                .Select(o => (DateTime?)o.CreatedAt)
                .FirstOrDefaultAsync();

            var pair = AnalyticsPeriodHelper.ResolvePair(
                filter.Preset, filter.From, filter.To, firstOrderDate: firstOrder);

            filter.Take = Math.Clamp(filter.Take <= 0 ? 50 : filter.Take, 1, 200);

            var hub = new SalesHubVm { Filter = filter, Periods = pair };

            var min = pair.Previous.StartInclusive;
            var max = pair.Current.EndExclusive;
            var rows = await _uow.Orders.Query().AsNoTracking()
                .Where(o => o.CreatedAt >= min && o.CreatedAt < max)
                .Select(o => new OrderRow(
                    o.Id,
                    o.CreatedAt,
                    o.Total,
                    o.Discount,
                    o.ShippingFee,
                    o.Subtotal,
                    o.PaymentStatus,
                    o.Status,
                    o.CancelReason))
                .ToListAsync();

            var cur = rows
                .Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Current))
                .Select(ToSnapshot)
                .ToList();
            var prev = rows
                .Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Previous))
                .Select(ToSnapshot)
                .ToList();

            switch (filter.Tab)
            {
                case SalesAnalyticsTab.Overview:
                    hub.Overview = BuildOverview(pair, cur, prev, rows);
                    break;
                case SalesAnalyticsTab.Merch:
                    hub.Merch = BuildMerchStub(filter);
                    break;
                case SalesAnalyticsTab.Cancellations:
                    hub.Cancellations = BuildCancellationsStub();
                    break;
            }

            return hub;
        }
        catch (ArgumentException ex)
        {
            return new SalesHubVm { Filter = filter, Error = ex.Message };
        }
    }

    public Task<byte[]> ExportExcelAsync(SalesAnalyticsFilterVm filter) =>
        throw new NotImplementedException("Excel export will be implemented in a later task.");

    private static OrderAnalyticsSnapshot ToSnapshot(OrderRow o) =>
        new(o.Total, o.PaymentStatus, o.Status, o.Discount, o.ShippingFee, o.Subtotal);

    private static SalesOverviewVm BuildOverview(
        AnalyticsPeriodPair pair,
        IReadOnlyList<OrderAnalyticsSnapshot> cur,
        IReadOnlyList<OrderAnalyticsSnapshot> prev,
        IReadOnlyList<OrderRow> rows)
    {
        var gross = SalesMetricEngine.Gross(cur);
        var prevGross = SalesMetricEngine.Gross(prev);
        var net = SalesMetricEngine.Net(cur);
        var prevNet = SalesMetricEngine.Net(prev);
        var paid = SalesMetricEngine.CountPaid(cur);
        var prevPaid = SalesMetricEngine.CountPaid(prev);
        var delivered = SalesMetricEngine.CountDelivered(cur);
        var prevDelivered = SalesMetricEngine.CountDelivered(prev);
        var aovNet = SalesMetricEngine.Aov(net, delivered);
        var prevAovNet = SalesMetricEngine.Aov(prevNet, prevDelivered);
        var cancelRate = SalesMetricEngine.CancelRatePercent(cur);
        var prevCancelRate = SalesMetricEngine.CancelRatePercent(prev);

        var curRows = rows
            .Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Current))
            .ToList();

        return new SalesOverviewVm
        {
            Gross = MetricValue.From(gross, prevGross),
            Net = MetricValue.From(net, prevNet),
            OrdersPaid = MetricValue.From(paid, prevPaid),
            AovNet = MetricValue.From(aovNet, prevAovNet),
            CancelRate = MetricValue.From(cancelRate, prevCancelRate),
            Trend = BuildGrossNetTrend(pair.Current, curRows),
            OrdersVolume = BuildOrdersVolume(pair.Current, curRows),
            CategoryMix = new ChartSeriesDto(),
            AovTrend = BuildAovTrend(pair.Current, curRows),
            UnitsTrend = new ChartSeriesDto(),
            Pipeline = BuildPipeline(curRows),
            PeriodCompare = BuildPeriodCompare(gross, prevGross, net, prevNet, paid, prevPaid),
            TopProductsBar = new ChartSeriesDto(),
            TopProducts = new List<MerchRankRowVm>(),
            TopCategories = new List<MerchRankRowVm>()
        };
    }

    private static ChartSeriesDto BuildGrossNetTrend(AnalyticsDateRange current, IReadOnlyList<OrderRow> curRows)
    {
        var labels = new List<string>();
        var grossData = new List<decimal>();
        var netData = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            var dayRows = curRows.Where(o => o.CreatedAt.Date == day).Select(ToSnapshot).ToList();
            grossData.Add(SalesMetricEngine.Gross(dayRows));
            netData.Add(SalesMetricEngine.Net(dayRows));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Gross", Data = grossData },
                new ChartDatasetDto { Label = "Net", Data = netData }
            }
        };
    }

    private static ChartSeriesDto BuildOrdersVolume(AnalyticsDateRange current, IReadOnlyList<OrderRow> curRows)
    {
        var labels = new List<string>();
        var paidData = new List<decimal>();
        var cancelData = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            var dayRows = curRows.Where(o => o.CreatedAt.Date == day).Select(ToSnapshot).ToList();
            paidData.Add(SalesMetricEngine.CountPaid(dayRows));
            cancelData.Add(SalesMetricEngine.CountCancelled(dayRows));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Paid", Data = paidData },
                new ChartDatasetDto { Label = "Cancelled", Data = cancelData }
            }
        };
    }

    private static ChartSeriesDto BuildAovTrend(AnalyticsDateRange current, IReadOnlyList<OrderRow> curRows)
    {
        var labels = new List<string>();
        var aovGross = new List<decimal>();
        var aovNet = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            var dayRows = curRows.Where(o => o.CreatedAt.Date == day).Select(ToSnapshot).ToList();
            var g = SalesMetricEngine.Gross(dayRows);
            var n = SalesMetricEngine.Net(dayRows);
            var paid = SalesMetricEngine.CountPaid(dayRows);
            var delivered = SalesMetricEngine.CountDelivered(dayRows);
            aovGross.Add(SalesMetricEngine.Aov(g, paid));
            aovNet.Add(SalesMetricEngine.Aov(n, delivered));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "AOV Gross", Data = aovGross },
                new ChartDatasetDto { Label = "AOV Net", Data = aovNet }
            }
        };
    }

    private static ChartSeriesDto BuildPipeline(IReadOnlyList<OrderRow> curRows)
    {
        var statuses = Enum.GetValues<OrderStatus>();
        var labels = statuses.Select(s => s.ToString()).ToList();
        var data = statuses.Select(s => (decimal)curRows.Count(o => o.Status == s)).ToList();

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Orders", Data = data }
            }
        };
    }

    private static ChartSeriesDto BuildPeriodCompare(
        decimal gross, decimal prevGross,
        decimal net, decimal prevNet,
        int paid, int prevPaid)
    {
        return new ChartSeriesDto
        {
            Labels = new List<string> { "Gross", "Net", "Orders" },
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Current",
                    Data = new List<decimal> { gross, net, paid }
                },
                new ChartDatasetDto
                {
                    Label = "Previous",
                    Data = new List<decimal> { prevGross, prevNet, prevPaid }
                }
            }
        };
    }

    private static SalesMerchVm BuildMerchStub(SalesAnalyticsFilterVm filter) =>
        new()
        {
            Dimension = filter.Dimension,
            Rows = new List<MerchRankRowVm>(),
            RankBar = new ChartSeriesDto(),
            CategoryMix = new ChartSeriesDto(),
            UnitsVsNet = new ChartSeriesDto(),
            Growth = new ChartSeriesDto()
        };

    private static SalesCancellationsVm BuildCancellationsStub() =>
        new()
        {
            CancelledCount = MetricValue.From(0, 0),
            CancelRate = MetricValue.From(0, 0),
            CancelledValue = MetricValue.From(0, 0),
            RefundRate = MetricValue.From(0, 0),
            CancelTrend = new ChartSeriesDto(),
            Reasons = new ChartSeriesDto(),
            ValueByProduct = new ChartSeriesDto(),
            ValueByCategory = new ChartSeriesDto()
        };

    private sealed record OrderRow(
        int Id,
        DateTime CreatedAt,
        decimal Total,
        decimal Discount,
        decimal ShippingFee,
        decimal Subtotal,
        PaymentStatus PaymentStatus,
        OrderStatus Status,
        string? CancelReason);
}
