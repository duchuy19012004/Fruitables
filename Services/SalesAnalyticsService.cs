using ClosedXML.Excel;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Analytics;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services;

public class SalesAnalyticsService : ISalesAnalyticsService
{
    private const string UnknownCancelReason = "Không ghi rõ";

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
                    hub.Overview = await BuildOverviewAsync(pair, cur, prev, rows);
                    break;
                case SalesAnalyticsTab.Merch:
                    hub.Merch = await BuildMerchAsync(filter, pair);
                    break;
                case SalesAnalyticsTab.Cancellations:
                    hub.Cancellations = await BuildCancellationsAsync(pair, cur, prev, rows);
                    break;
            }

            return hub;
        }
        catch (ArgumentException ex)
        {
            return new SalesHubVm { Filter = filter, Error = ex.Message };
        }
    }

    public async Task<byte[]> ExportExcelAsync(SalesAnalyticsFilterVm filter)
    {
        var hub = await GetHubAsync(filter);
        if (!string.IsNullOrEmpty(hub.Error))
            throw new InvalidOperationException(hub.Error);

        // Always include overview KPIs even when exporting merch/cancel tabs.
        SalesOverviewVm? overview = hub.Overview;
        if (overview is null)
        {
            var ovFilter = CloneFilter(filter, SalesAnalyticsTab.Overview);
            var ovHub = await GetHubAsync(ovFilter);
            overview = ovHub.Overview;
        }

        using var wb = new XLWorkbook();
        WriteMetricDefinitionsSheet(wb, hub);
        WriteKpiSheet(wb, hub, overview);
        WriteTabDataSheet(wb, hub);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static SalesAnalyticsFilterVm CloneFilter(SalesAnalyticsFilterVm src, SalesAnalyticsTab tab) =>
        new()
        {
            Preset = src.Preset,
            From = src.From,
            To = src.To,
            Tab = tab,
            Dimension = src.Dimension,
            Sort = src.Sort,
            Dir = src.Dir,
            Take = src.Take
        };

    private static void WriteMetricDefinitionsSheet(XLWorkbook wb, SalesHubVm hub)
    {
        var ws = wb.Worksheets.Add("Định nghĩa metric");
        ws.Cell(1, 1).Value = "Metric";
        ws.Cell(1, 2).Value = "Định nghĩa (v1)";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var rows = new (string Metric, string Def)[]
        {
            ("Gross revenue", "Sum(Total) của đơn Paid (PaymentStatus == Paid)"),
            ("Net revenue", "Sum(Total) Delivered+Paid − Sum(Total) Refunded"),
            ("Orders paid", "Số đơn thuộc Paid set"),
            ("AOV gross", "Gross / count(Paid); 0 nếu không có đơn"),
            ("AOV net", "Net / count(Delivered); 0 nếu không có đơn"),
            ("Cancel rate", "count(Cancelled) / count(All orders) × 100"),
            ("Cancelled value", "Sum(Total) của đơn Cancelled"),
            ("Refund rate", "count(Refund) / count(Paid) × 100; 0 nếu Paid = 0"),
            ("Merch Net (line)", "Price × Quantity trên dòng OrderItem của đơn Delivered+Paid"),
            ("Share %", "Line Net / tổng Line Net trong kỳ"),
            ("Time basis", "Order.CreatedAt (giờ cửa hàng VN); không dùng ngày paid/delivered"),
            ("Kỳ hiện tại", hub.Periods.Current.Label),
            ("Kỳ so sánh", hub.Periods.Previous.Label),
            ("Tab xuất", hub.Filter.Tab.ToString()),
            ("Dimension", hub.Filter.Dimension.ToString())
        };

        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].Metric;
            ws.Cell(i + 2, 2).Value = rows[i].Def;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteKpiSheet(XLWorkbook wb, SalesHubVm hub, SalesOverviewVm? overview)
    {
        var ws = wb.Worksheets.Add("KPI");
        ws.Cell(1, 1).Value = "KPI";
        ws.Cell(1, 2).Value = "Hiện tại";
        ws.Cell(1, 3).Value = "Kỳ trước";
        ws.Cell(1, 4).Value = "Δ";
        ws.Cell(1, 5).Value = "Δ %";
        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "Kỳ hiện tại";
        ws.Cell(2, 2).Value = hub.Periods.Current.Label;
        ws.Cell(3, 1).Value = "Kỳ so sánh";
        ws.Cell(3, 2).Value = hub.Periods.Previous.Label;

        var row = 5;
        void WriteMetric(string name, MetricValue m)
        {
            ws.Cell(row, 1).Value = name;
            ws.Cell(row, 2).Value = m.Value;
            ws.Cell(row, 3).Value = m.Previous ?? 0;
            ws.Cell(row, 4).Value = m.Delta ?? 0;
            if (m.DeltaPercent is null)
                ws.Cell(row, 5).Value = "—";
            else
                ws.Cell(row, 5).Value = m.DeltaPercent.Value;
            row++;
        }

        if (overview is not null)
        {
            WriteMetric("Gross", overview.Gross);
            WriteMetric("Net", overview.Net);
            WriteMetric("Orders paid", overview.OrdersPaid);
            WriteMetric("AOV net", overview.AovNet);
            WriteMetric("Cancel rate %", overview.CancelRate);
        }

        if (hub.Cancellations is { } c)
        {
            WriteMetric("Cancelled count", c.CancelledCount);
            WriteMetric("Cancel rate % (tab)", c.CancelRate);
            WriteMetric("Cancelled value", c.CancelledValue);
            WriteMetric("Refund rate %", c.RefundRate);
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteTabDataSheet(XLWorkbook wb, SalesHubVm hub)
    {
        switch (hub.Filter.Tab)
        {
            case SalesAnalyticsTab.Merch when hub.Merch is not null:
                WriteMerchSheet(wb, hub.Merch);
                break;
            case SalesAnalyticsTab.Cancellations when hub.Cancellations is not null:
                WriteCancelReasonsSheet(wb, hub.Cancellations);
                break;
            default:
                WriteOverviewTablesSheet(wb, hub.Overview);
                break;
        }
    }

    private static void WriteMerchSheet(XLWorkbook wb, SalesMerchVm merch)
    {
        var ws = wb.Worksheets.Add("Merch ranking");
        var headers = new[] { "#", "Name", "Category", "Units", "Net", "Share %", "Orders", "Δ %" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        var r = 2;
        foreach (var row in merch.Rows)
        {
            ws.Cell(r, 1).Value = row.Rank;
            ws.Cell(r, 2).Value = row.Name;
            ws.Cell(r, 3).Value = row.CategoryName ?? "";
            ws.Cell(r, 4).Value = row.Units;
            ws.Cell(r, 5).Value = row.NetRevenue;
            ws.Cell(r, 6).Value = row.SharePercent;
            ws.Cell(r, 7).Value = row.OrderCount;
            if (row.DeltaPercent is null)
                ws.Cell(r, 8).Value = "—";
            else
                ws.Cell(r, 8).Value = row.DeltaPercent.Value;
            r++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteCancelReasonsSheet(XLWorkbook wb, SalesCancellationsVm cancel)
    {
        var ws = wb.Worksheets.Add("Cancel reasons");
        ws.Cell(1, 1).Value = "Reason";
        ws.Cell(1, 2).Value = "Count";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var labels = cancel.Reasons.Labels;
        var data = cancel.Reasons.Datasets.FirstOrDefault()?.Data ?? new List<decimal>();
        for (var i = 0; i < labels.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = labels[i];
            ws.Cell(i + 2, 2).Value = i < data.Count ? data[i] : 0;
        }

        // Also dump value-by-product for depth
        var ws2 = wb.Worksheets.Add("Cancel by product");
        ws2.Cell(1, 1).Value = "Product";
        ws2.Cell(1, 2).Value = "Value";
        ws2.Range(1, 1, 1, 2).Style.Font.Bold = true;
        var pLabels = cancel.ValueByProduct.Labels;
        var pData = cancel.ValueByProduct.Datasets.FirstOrDefault()?.Data ?? new List<decimal>();
        for (var i = 0; i < pLabels.Count; i++)
        {
            ws2.Cell(i + 2, 1).Value = pLabels[i];
            ws2.Cell(i + 2, 2).Value = i < pData.Count ? pData[i] : 0;
        }

        ws.Columns().AdjustToContents();
        ws2.Columns().AdjustToContents();
    }

    private static void WriteOverviewTablesSheet(XLWorkbook wb, SalesOverviewVm? overview)
    {
        var ws = wb.Worksheets.Add("Top products");
        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "Product";
        ws.Cell(1, 3).Value = "Category";
        ws.Cell(1, 4).Value = "Units";
        ws.Cell(1, 5).Value = "Net";
        ws.Cell(1, 6).Value = "Share %";
        ws.Cell(1, 7).Value = "Δ %";
        ws.Range(1, 1, 1, 7).Style.Font.Bold = true;

        if (overview is not null)
        {
            var r = 2;
            foreach (var row in overview.TopProducts)
            {
                ws.Cell(r, 1).Value = row.Rank;
                ws.Cell(r, 2).Value = row.Name;
                ws.Cell(r, 3).Value = row.CategoryName ?? "";
                ws.Cell(r, 4).Value = row.Units;
                ws.Cell(r, 5).Value = row.NetRevenue;
                ws.Cell(r, 6).Value = row.SharePercent;
                if (row.DeltaPercent is null)
                    ws.Cell(r, 7).Value = "—";
                else
                    ws.Cell(r, 7).Value = row.DeltaPercent.Value;
                r++;
            }

            var wsCat = wb.Worksheets.Add("Top categories");
            wsCat.Cell(1, 1).Value = "#";
            wsCat.Cell(1, 2).Value = "Category";
            wsCat.Cell(1, 3).Value = "Units";
            wsCat.Cell(1, 4).Value = "Net";
            wsCat.Cell(1, 5).Value = "Share %";
            wsCat.Cell(1, 6).Value = "Δ %";
            wsCat.Range(1, 1, 1, 6).Style.Font.Bold = true;
            var cr = 2;
            foreach (var row in overview.TopCategories)
            {
                wsCat.Cell(cr, 1).Value = row.Rank;
                wsCat.Cell(cr, 2).Value = row.Name;
                wsCat.Cell(cr, 3).Value = row.Units;
                wsCat.Cell(cr, 4).Value = row.NetRevenue;
                wsCat.Cell(cr, 5).Value = row.SharePercent;
                if (row.DeltaPercent is null)
                    wsCat.Cell(cr, 6).Value = "—";
                else
                    wsCat.Cell(cr, 6).Value = row.DeltaPercent.Value;
                cr++;
            }

            wsCat.Columns().AdjustToContents();
        }

        ws.Columns().AdjustToContents();
    }

    private static OrderAnalyticsSnapshot ToSnapshot(OrderRow o) =>
        new(o.Total, o.PaymentStatus, o.Status, o.Discount, o.ShippingFee, o.Subtotal);

    private async Task<SalesOverviewVm> BuildOverviewAsync(
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

        var lineItems = await LoadDeliveredLineItemsAsync(pair.Previous.StartInclusive, pair.Current.EndExclusive);
        var curLines = lineItems.Where(i => AnalyticsPeriodHelper.InRange(i.OrderCreatedAt, pair.Current)).ToList();
        var prevLines = lineItems.Where(i => AnalyticsPeriodHelper.InRange(i.OrderCreatedAt, pair.Previous)).ToList();

        var topProducts = BuildProductRanks(curLines, prevLines, take: 5);
        var topCategories = BuildCategoryRanks(curLines, prevLines, take: 5);

        return new SalesOverviewVm
        {
            Gross = MetricValue.From(gross, prevGross),
            Net = MetricValue.From(net, prevNet),
            OrdersPaid = MetricValue.From(paid, prevPaid),
            AovNet = MetricValue.From(aovNet, prevAovNet),
            CancelRate = MetricValue.From(cancelRate, prevCancelRate),
            Trend = BuildGrossNetTrend(pair.Current, curRows),
            OrdersVolume = BuildOrdersVolume(pair.Current, curRows),
            CategoryMix = BuildCategoryMixChart(curLines),
            AovTrend = BuildAovTrend(pair.Current, curRows),
            UnitsTrend = BuildUnitsTrend(pair.Current, curLines),
            Pipeline = BuildPipeline(curRows),
            PeriodCompare = BuildPeriodCompare(gross, prevGross, net, prevNet, paid, prevPaid),
            TopProductsBar = BuildRankBar(topProducts, "Net"),
            TopProducts = topProducts,
            TopCategories = topCategories
        };
    }

    private async Task<SalesMerchVm> BuildMerchAsync(
        SalesAnalyticsFilterVm filter,
        AnalyticsPeriodPair pair)
    {
        var lineItems = await LoadDeliveredLineItemsAsync(pair.Previous.StartInclusive, pair.Current.EndExclusive);
        var curLines = lineItems.Where(i => AnalyticsPeriodHelper.InRange(i.OrderCreatedAt, pair.Current)).ToList();
        var prevLines = lineItems.Where(i => AnalyticsPeriodHelper.InRange(i.OrderCreatedAt, pair.Previous)).ToList();

        var rows = filter.Dimension == MerchDimension.Category
            ? BuildCategoryRanks(curLines, prevLines, filter.Take)
            : BuildProductRanks(curLines, prevLines, filter.Take);

        return new SalesMerchVm
        {
            Dimension = filter.Dimension,
            Rows = rows,
            RankBar = BuildRankBar(rows, "Net"),
            CategoryMix = BuildCategoryMixChart(curLines),
            UnitsVsNet = BuildUnitsVsNet(rows),
            Growth = BuildGrowthChart(rows)
        };
    }

    private async Task<SalesCancellationsVm> BuildCancellationsAsync(
        AnalyticsPeriodPair pair,
        IReadOnlyList<OrderAnalyticsSnapshot> cur,
        IReadOnlyList<OrderAnalyticsSnapshot> prev,
        IReadOnlyList<OrderRow> rows)
    {
        var curRows = rows
            .Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Current))
            .ToList();
        var prevRows = rows
            .Where(o => AnalyticsPeriodHelper.InRange(o.CreatedAt, pair.Previous))
            .ToList();

        var cancelledCount = SalesMetricEngine.CountCancelled(cur);
        var prevCancelledCount = SalesMetricEngine.CountCancelled(prev);
        var cancelRate = SalesMetricEngine.CancelRatePercent(cur);
        var prevCancelRate = SalesMetricEngine.CancelRatePercent(prev);
        var cancelledValue = curRows.Where(o => o.Status == OrderStatus.Cancelled).Sum(o => o.Total);
        var prevCancelledValue = prevRows.Where(o => o.Status == OrderStatus.Cancelled).Sum(o => o.Total);
        var refundRate = SalesMetricEngine.RefundRatePercent(cur);
        var prevRefundRate = SalesMetricEngine.RefundRatePercent(prev);

        var cancelLines = await LoadCancelledLineItemsAsync(
            pair.Current.StartInclusive, pair.Current.EndExclusive);

        return new SalesCancellationsVm
        {
            CancelledCount = MetricValue.From(cancelledCount, prevCancelledCount),
            CancelRate = MetricValue.From(cancelRate, prevCancelRate),
            CancelledValue = MetricValue.From(cancelledValue, prevCancelledValue),
            RefundRate = MetricValue.From(refundRate, prevRefundRate),
            CancelTrend = BuildCancelTrend(pair.Current, curRows),
            Reasons = BuildCancelReasonsChart(curRows),
            ValueByProduct = BuildCancelValueByProductChart(cancelLines),
            ValueByCategory = BuildCancelValueByCategoryChart(cancelLines)
        };
    }

    private async Task<List<LineItemRow>> LoadDeliveredLineItemsAsync(DateTime min, DateTime max)
    {
        return await _uow.OrderItems.Query().AsNoTracking()
            .Where(i =>
                i.Order.CreatedAt >= min &&
                i.Order.CreatedAt < max &&
                i.Order.PaymentStatus == PaymentStatus.Paid &&
                i.Order.Status == OrderStatus.Delivered)
            .Select(i => new LineItemRow(
                i.OrderId,
                i.Order.CreatedAt,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.Price,
                i.Product.CategoryId,
                i.Product.Category.Name))
            .ToListAsync();
    }

    private async Task<List<LineItemRow>> LoadCancelledLineItemsAsync(DateTime min, DateTime max)
    {
        return await _uow.OrderItems.Query().AsNoTracking()
            .Where(i =>
                i.Order.CreatedAt >= min &&
                i.Order.CreatedAt < max &&
                i.Order.Status == OrderStatus.Cancelled)
            .Select(i => new LineItemRow(
                i.OrderId,
                i.Order.CreatedAt,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.Price,
                i.Product.CategoryId,
                i.Product.Category.Name))
            .ToListAsync();
    }

    private static List<MerchRankRowVm> BuildProductRanks(
        IReadOnlyList<LineItemRow> current,
        IReadOnlyList<LineItemRow> previous,
        int take)
    {
        var prevNet = previous
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.LineNet));

        var groups = current
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    ProductId = g.Key,
                    Name = first.ProductName,
                    CategoryId = first.CategoryId,
                    CategoryName = first.CategoryName,
                    Net = g.Sum(x => x.LineNet),
                    Units = g.Sum(x => x.Quantity),
                    OrderCount = g.Select(x => x.OrderId).Distinct().Count()
                };
            })
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Name)
            .ToList();

        var totalNet = groups.Sum(g => g.Net);
        return groups
            .Take(take)
            .Select((g, idx) => new MerchRankRowVm
            {
                Rank = idx + 1,
                ProductId = g.ProductId,
                CategoryId = g.CategoryId,
                Name = g.Name,
                CategoryName = g.CategoryName,
                Units = g.Units,
                NetRevenue = g.Net,
                SharePercent = SharePercent(g.Net, totalNet),
                OrderCount = g.OrderCount,
                DeltaPercent = MetricValue.From(g.Net, prevNet.GetValueOrDefault(g.ProductId)).DeltaPercent
            })
            .ToList();
    }

    private static List<MerchRankRowVm> BuildCategoryRanks(
        IReadOnlyList<LineItemRow> current,
        IReadOnlyList<LineItemRow> previous,
        int take)
    {
        var prevNet = previous
            .GroupBy(i => i.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.LineNet));

        var groups = current
            .GroupBy(i => i.CategoryId)
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    CategoryId = g.Key,
                    Name = first.CategoryName,
                    Net = g.Sum(x => x.LineNet),
                    Units = g.Sum(x => x.Quantity),
                    OrderCount = g.Select(x => x.OrderId).Distinct().Count()
                };
            })
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Name)
            .ToList();

        var totalNet = groups.Sum(g => g.Net);
        return groups
            .Take(take)
            .Select((g, idx) => new MerchRankRowVm
            {
                Rank = idx + 1,
                ProductId = null,
                CategoryId = g.CategoryId,
                Name = g.Name,
                CategoryName = g.Name,
                Units = g.Units,
                NetRevenue = g.Net,
                SharePercent = SharePercent(g.Net, totalNet),
                OrderCount = g.OrderCount,
                DeltaPercent = MetricValue.From(g.Net, prevNet.GetValueOrDefault(g.CategoryId)).DeltaPercent
            })
            .ToList();
    }

    private static decimal SharePercent(decimal part, decimal total) =>
        total == 0 ? 0 : Math.Round(part / total * 100m, 2);

    private static ChartSeriesDto BuildRankBar(IReadOnlyList<MerchRankRowVm> rows, string datasetLabel) =>
        new()
        {
            Labels = rows.Select(r => r.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = datasetLabel,
                    Data = rows.Select(r => r.NetRevenue).ToList()
                }
            }
        };

    private static ChartSeriesDto BuildCategoryMixChart(IReadOnlyList<LineItemRow> lines)
    {
        var groups = lines
            .GroupBy(i => i.CategoryName)
            .Select(g => new { Name = g.Key, Net = g.Sum(x => x.LineNet) })
            .OrderByDescending(g => g.Net)
            .ToList();

        return new ChartSeriesDto
        {
            Labels = groups.Select(g => g.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Net",
                    Data = groups.Select(g => g.Net).ToList()
                }
            }
        };
    }

    private static ChartSeriesDto BuildUnitsVsNet(IReadOnlyList<MerchRankRowVm> rows) =>
        new()
        {
            Labels = rows.Select(r => r.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Units",
                    Data = rows.Select(r => (decimal)r.Units).ToList()
                },
                new ChartDatasetDto
                {
                    Label = "Net",
                    Data = rows.Select(r => r.NetRevenue).ToList()
                }
            }
        };

    private static ChartSeriesDto BuildGrowthChart(IReadOnlyList<MerchRankRowVm> rows) =>
        new()
        {
            Labels = rows.Select(r => r.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Delta %",
                    Data = rows.Select(r => r.DeltaPercent ?? 0m).ToList()
                }
            }
        };

    private static ChartSeriesDto BuildUnitsTrend(AnalyticsDateRange current, IReadOnlyList<LineItemRow> curLines)
    {
        var labels = new List<string>();
        var data = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            data.Add(curLines.Where(i => i.OrderCreatedAt.Date == day).Sum(i => i.Quantity));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Units", Data = data }
            }
        };
    }

    private static ChartSeriesDto BuildCancelTrend(AnalyticsDateRange current, IReadOnlyList<OrderRow> curRows)
    {
        var labels = new List<string>();
        var data = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            data.Add(curRows.Count(o => o.CreatedAt.Date == day && o.Status == OrderStatus.Cancelled));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Cancelled", Data = data }
            }
        };
    }

    private static ChartSeriesDto BuildCancelReasonsChart(IReadOnlyList<OrderRow> curRows)
    {
        var groups = curRows
            .Where(o => o.Status == OrderStatus.Cancelled)
            .GroupBy(o => string.IsNullOrWhiteSpace(o.CancelReason) ? UnknownCancelReason : o.CancelReason!.Trim())
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Reason)
            .ToList();

        return new ChartSeriesDto
        {
            Labels = groups.Select(g => g.Reason).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Count",
                    Data = groups.Select(g => (decimal)g.Count).ToList()
                }
            }
        };
    }

    private static ChartSeriesDto BuildCancelValueByProductChart(IReadOnlyList<LineItemRow> lines)
    {
        var groups = lines
            .GroupBy(i => i.ProductName)
            .Select(g => new { Name = g.Key, Net = g.Sum(x => x.LineNet) })
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Name)
            .ToList();

        return new ChartSeriesDto
        {
            Labels = groups.Select(g => g.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Value",
                    Data = groups.Select(g => g.Net).ToList()
                }
            }
        };
    }

    private static ChartSeriesDto BuildCancelValueByCategoryChart(IReadOnlyList<LineItemRow> lines)
    {
        var groups = lines
            .GroupBy(i => i.CategoryName)
            .Select(g => new { Name = g.Key, Net = g.Sum(x => x.LineNet) })
            .OrderByDescending(g => g.Net)
            .ThenBy(g => g.Name)
            .ToList();

        return new ChartSeriesDto
        {
            Labels = groups.Select(g => g.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Value",
                    Data = groups.Select(g => g.Net).ToList()
                }
            }
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

    private sealed record LineItemRow(
        int OrderId,
        DateTime OrderCreatedAt,
        int ProductId,
        string ProductName,
        int Quantity,
        decimal Price,
        int CategoryId,
        string CategoryName)
    {
        public decimal LineNet => Price * Quantity;
    }
}
