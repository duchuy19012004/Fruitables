using Fruitables.Data;
using ClosedXML.Excel;
using Fruitables.Models;
using Fruitables.Models.Returns;
using Fruitables.Services.Analytics.Common;
using Fruitables.Services.Communications;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Analytics.Sales;

public class SalesAnalyticsService : ISalesAnalyticsService
{
    private const string UnknownCancelReason = "Không ghi rõ";

    private readonly ApplicationDbContext _db;

    public SalesAnalyticsService(ApplicationDbContext db) => _db = db;

    public async Task<SalesHubVm> GetHubAsync(SalesAnalyticsFilterVm filter)
    {
        try
        {
            var firstOrder = await _db.Orders.AsNoTracking()
                .OrderBy(o => o.CreatedAt)
                .Select(o => (DateTime?)o.CreatedAt)
                .FirstOrDefaultAsync();

            var pair = AnalyticsPeriodHelper.ResolvePair(
                filter.Preset, filter.From, filter.To, firstOrderDate: firstOrder);

            filter.Take = Math.Clamp(filter.Take <= 0 ? 50 : filter.Take, 1, 200);

            var hub = new SalesHubVm { Filter = filter, Periods = pair };

            var min = pair.Previous.StartInclusive;
            var max = pair.Current.EndExclusive;
            var rows = await _db.Orders.AsNoTracking()
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
                    o.CancelReason,
                    o.ReturnRequest != null && o.ReturnRequest.Refund != null &&
                    o.ReturnRequest.Refund.Status == RefundStatus.Succeeded
                        ? o.ReturnRequest.Refund.Amount
                        : 0m))
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
            throw new ArgumentException(hub.Error);

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
        ws.Cell(1, 1).Value = "Chỉ số";
        ws.Cell(1, 2).Value = "Định nghĩa (v1)";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var rows = new (string Metric, string Def)[]
        {
            ("Doanh thu gộp", "Tổng tiền đơn đã thanh toán (PaymentStatus = Paid)"),
            ("Doanh thu thuần", "Tổng tiền đơn đã giao & đã thanh toán trừ đơn đã hoàn tiền"),
            ("Đơn đã thanh toán", "Số đơn thuộc tập đã thanh toán"),
            ("GTB đơn gộp", "Doanh thu gộp / số đơn đã thanh toán; 0 nếu không có đơn"),
            ("GTB đơn thuần", "Doanh thu thuần / số đơn đã giao; 0 nếu không có đơn"),
            ("Tỷ lệ hủy", "Số đơn hủy / tổng đơn trong kỳ × 100"),
            ("Giá trị hủy", "Tổng tiền các đơn đã hủy"),
            ("Tỷ lệ hoàn tiền", "Số đơn hoàn / số đơn đã thanh toán × 100; 0 nếu không có đơn paid"),
            ("Doanh thu thuần theo dòng", "Giá × số lượng trên dòng hàng của đơn đã giao & đã thanh toán"),
            ("Tỷ trọng %", "Doanh thu thuần dòng / tổng doanh thu thuần dòng trong kỳ"),
            ("Mốc thời gian", "Ngày tạo đơn (giờ VN); không dùng ngày thanh toán/giao hàng"),
            ("Kỳ hiện tại", hub.Periods.Current.Label),
            ("Kỳ so sánh", hub.Periods.Previous.Label),
            ("Tab xuất", hub.Filter.Tab switch
            {
                SalesAnalyticsTab.Merch => "Sản phẩm & danh mục",
                SalesAnalyticsTab.Cancellations => "Hủy & hoàn",
                _ => "Tổng quan"
            }),
            ("Chiều xếp hạng", hub.Filter.Dimension == MerchDimension.Category ? "Danh mục" : "Sản phẩm")
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
            WriteMetric("Doanh thu gộp", overview.Gross);
            WriteMetric("Doanh thu thuần", overview.Net);
            WriteMetric("Đơn đã thanh toán", overview.OrdersPaid);
            WriteMetric("GTB đơn thuần", overview.AovNet);
            WriteMetric("Tỷ lệ hủy %", overview.CancelRate);
        }

        if (hub.Cancellations is { } c)
        {
            WriteMetric("Số đơn hủy", c.CancelledCount);
            WriteMetric("Tỷ lệ hủy % (tab)", c.CancelRate);
            WriteMetric("Giá trị hủy", c.CancelledValue);
            WriteMetric("Tỷ lệ hoàn tiền %", c.RefundRate);
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
        var ws = wb.Worksheets.Add("Xếp hạng");
        var headers = new[] { "#", "Tên", "Danh mục", "SL", "Thuần", "Tỷ trọng %", "Đơn", "Δ %" };
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
        var ws = wb.Worksheets.Add("Lý do hủy");
        ws.Cell(1, 1).Value = "Lý do";
        ws.Cell(1, 2).Value = "Số đơn";
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var labels = cancel.Reasons.Labels;
        var data = cancel.Reasons.Datasets.FirstOrDefault()?.Data ?? new List<decimal>();
        for (var i = 0; i < labels.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = labels[i];
            ws.Cell(i + 2, 2).Value = i < data.Count ? data[i] : 0;
        }

        // Also dump value-by-product for depth
        var ws2 = wb.Worksheets.Add("Hủy theo sản phẩm");
        ws2.Cell(1, 1).Value = "Sản phẩm";
        ws2.Cell(1, 2).Value = "Giá trị";
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
        var ws = wb.Worksheets.Add("Top sản phẩm");
        ws.Cell(1, 1).Value = "#";
        ws.Cell(1, 2).Value = "Sản phẩm";
        ws.Cell(1, 3).Value = "Danh mục";
        ws.Cell(1, 4).Value = "SL";
        ws.Cell(1, 5).Value = "Thuần";
        ws.Cell(1, 6).Value = "Tỷ trọng %";
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

            var wsCat = wb.Worksheets.Add("Top danh mục");
            wsCat.Cell(1, 1).Value = "#";
            wsCat.Cell(1, 2).Value = "Danh mục";
            wsCat.Cell(1, 3).Value = "SL";
            wsCat.Cell(1, 4).Value = "Thuần";
            wsCat.Cell(1, 5).Value = "Tỷ trọng %";
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
        new(o.Total, o.PaymentStatus, o.Status, o.Discount, o.ShippingFee, o.Subtotal, o.SuccessfulRefundAmount);

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
            TopProductsBar = BuildRankBar(topProducts, "Thuần"),
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
            ? BuildCategoryRanks(curLines, prevLines, filter.Take, filter.Sort, filter.Dir)
            : BuildProductRanks(curLines, prevLines, filter.Take, filter.Sort, filter.Dir);

        return new SalesMerchVm
        {
            Dimension = filter.Dimension,
            Rows = rows,
            RankBar = BuildRankBar(rows, "Thuần"),
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
        var rows = await _db.OrderItems.AsNoTracking()
            .Where(i =>
                i.Order.CreatedAt >= min &&
                i.Order.CreatedAt < max &&
                i.Order.PaymentStatus == PaymentStatus.Paid &&
                i.Order.Status == OrderStatus.Delivered)
            .Select(i => new LineItemRow(
                i.Id,
                i.OrderId,
                i.Order.CreatedAt,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.Price,
                0m,
                i.Product.CategoryId,
                i.Product.Category.Name))
            .ToListAsync();
        if (rows.Count == 0)
            return rows;

        var orderItemIds = rows.Select(row => row.OrderItemId).ToArray();
        var refundRows = await _db.OrderItems.AsNoTracking()
            .Where(item => orderItemIds.Contains(item.Id))
            .SelectMany(item => item.ReturnRequestItems)
            .Where(item => item.ReturnRequest.Refund != null &&
                item.ReturnRequest.Refund.Status == RefundStatus.Succeeded)
            .Select(item => new { item.OrderItemId, item.ApprovedAmount })
            .ToListAsync();
        var refundsByItem = refundRows
            .GroupBy(item => item.OrderItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.ApprovedAmount));

        return rows.Select(row => row with
        {
            RefundedAmount = refundsByItem.GetValueOrDefault(row.OrderItemId)
        }).ToList();
    }

    private async Task<List<LineItemRow>> LoadCancelledLineItemsAsync(DateTime min, DateTime max)
    {
        return await _db.OrderItems.AsNoTracking()
            .Where(i =>
                i.Order.CreatedAt >= min &&
                i.Order.CreatedAt < max &&
                i.Order.Status == OrderStatus.Cancelled)
            .Select(i => new LineItemRow(
                i.Id,
                i.OrderId,
                i.Order.CreatedAt,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.Price,
                0m,
                i.Product.CategoryId,
                i.Product.Category.Name))
            .ToListAsync();
    }

    private static List<MerchRankRowVm> BuildProductRanks(
        IReadOnlyList<LineItemRow> current,
        IReadOnlyList<LineItemRow> previous,
        int take,
        string? sort = null,
        string? dir = null)
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
            .ToList();

        var totalNet = groups.Sum(g => g.Net);
        var rows = groups
            .Select(g => new MerchRankRowVm
            {
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

        return ApplyMerchSort(rows, sort, dir, take);
    }

    private static List<MerchRankRowVm> BuildCategoryRanks(
        IReadOnlyList<LineItemRow> current,
        IReadOnlyList<LineItemRow> previous,
        int take,
        string? sort = null,
        string? dir = null)
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
            .ToList();

        var totalNet = groups.Sum(g => g.Net);
        var rows = groups
            .Select(g => new MerchRankRowVm
            {
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

        return ApplyMerchSort(rows, sort, dir, take);
    }

    /// <summary>
    /// Sort merch ranking by filter key then take top N and assign ranks.
    /// Keys: net (default), units, share, orders, delta, name. Dir: asc|desc (default desc).
    /// </summary>
    private static List<MerchRankRowVm> ApplyMerchSort(
        IReadOnlyList<MerchRankRowVm> rows,
        string? sort,
        string? dir,
        int take)
    {
        var key = (sort ?? "net").Trim().ToLowerInvariant();
        var asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<MerchRankRowVm> ordered = key switch
        {
            "units" => asc
                ? rows.OrderBy(r => r.Units).ThenBy(r => r.Name)
                : rows.OrderByDescending(r => r.Units).ThenBy(r => r.Name),
            "share" => asc
                ? rows.OrderBy(r => r.SharePercent).ThenBy(r => r.Name)
                : rows.OrderByDescending(r => r.SharePercent).ThenBy(r => r.Name),
            "orders" => asc
                ? rows.OrderBy(r => r.OrderCount).ThenBy(r => r.Name)
                : rows.OrderByDescending(r => r.OrderCount).ThenBy(r => r.Name),
            "delta" => asc
                ? rows.OrderBy(r => r.DeltaPercent ?? decimal.MaxValue).ThenBy(r => r.Name)
                : rows.OrderByDescending(r => r.DeltaPercent ?? decimal.MinValue).ThenBy(r => r.Name),
            "name" => asc
                ? rows.OrderBy(r => r.Name)
                : rows.OrderByDescending(r => r.Name),
            _ => asc
                ? rows.OrderBy(r => r.NetRevenue).ThenBy(r => r.Name)
                : rows.OrderByDescending(r => r.NetRevenue).ThenBy(r => r.Name)
        };

        return ordered
            .Take(take)
            .Select((r, idx) =>
            {
                r.Rank = idx + 1;
                return r;
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
                    Label = "Doanh thu thuần",
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
                    Label = "Số lượng",
                    Data = rows.Select(r => (decimal)r.Units).ToList()
                },
                new ChartDatasetDto
                {
                    Label = "Doanh thu thuần",
                    Data = rows.Select(r => r.NetRevenue).ToList()
                }
            }
        };

    private static ChartSeriesDto BuildGrowthChart(IReadOnlyList<MerchRankRowVm> rows)
    {
        // Skip new products (null delta) — do not plot as 0% growth.
        var plotted = rows.Where(r => r.DeltaPercent.HasValue).ToList();
        return new ChartSeriesDto
        {
            Labels = plotted.Select(r => r.Name).ToList(),
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Δ %",
                    Data = plotted.Select(r => r.DeltaPercent!.Value).ToList()
                }
            }
        };
    }

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
                new ChartDatasetDto { Label = "Số lượng", Data = data }
            }
        };
    }

    private static ChartSeriesDto BuildCancelTrend(AnalyticsDateRange current, IReadOnlyList<OrderRow> curRows)
    {
        var labels = new List<string>();
        var countData = new List<decimal>();
        var rateData = new List<decimal>();

        for (var day = current.StartInclusive.Date; day < current.EndExclusive.Date; day = day.AddDays(1))
        {
            labels.Add(day.ToString("dd/MM"));
            var dayOrders = curRows.Where(o => o.CreatedAt.Date == day).ToList();
            var cancelled = dayOrders.Count(o => o.Status == OrderStatus.Cancelled);
            var all = dayOrders.Count;
            countData.Add(cancelled);
            rateData.Add(all == 0 ? 0m : Math.Round(cancelled / (decimal)all * 100m, 2));
        }

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Đơn hủy", Data = countData },
                new ChartDatasetDto { Label = "Tỷ lệ hủy %", Data = rateData }
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
                    Label = "Số đơn",
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
                    Label = "Giá trị",
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
                    Label = "Giá trị",
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
                new ChartDatasetDto { Label = "Doanh thu gộp", Data = grossData },
                new ChartDatasetDto { Label = "Doanh thu thuần", Data = netData }
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
                new ChartDatasetDto { Label = "Đã thanh toán", Data = paidData },
                new ChartDatasetDto { Label = "Đã hủy", Data = cancelData }
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
                new ChartDatasetDto { Label = "GTB gộp", Data = aovGross },
                new ChartDatasetDto { Label = "GTB thuần", Data = aovNet }
            }
        };
    }

    private static ChartSeriesDto BuildPipeline(IReadOnlyList<OrderRow> curRows)
    {
        var statuses = Enum.GetValues<OrderStatus>();
        var labels = statuses.Select(StatusDisplayName).ToList();
        var data = statuses.Select(s => (decimal)curRows.Count(o => o.Status == s)).ToList();

        return new ChartSeriesDto
        {
            Labels = labels,
            Datasets =
            {
                new ChartDatasetDto { Label = "Số đơn", Data = data }
            }
        };
    }

    private static string StatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xử lý",
        OrderStatus.Processing => "Đang xử lý",
        OrderStatus.Shipped => "Đang giao",
        OrderStatus.Delivered => "Đã giao",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    private static ChartSeriesDto BuildPeriodCompare(
        decimal gross, decimal prevGross,
        decimal net, decimal prevNet,
        int paid, int prevPaid)
    {
        return new ChartSeriesDto
        {
            Labels = new List<string> { "Doanh thu gộp", "Doanh thu thuần", "Đơn" },
            Datasets =
            {
                new ChartDatasetDto
                {
                    Label = "Kỳ này",
                    Data = new List<decimal> { gross, net, paid }
                },
                new ChartDatasetDto
                {
                    Label = "Kỳ trước",
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
        string? CancelReason,
        decimal SuccessfulRefundAmount);

    private sealed record LineItemRow(
        int OrderItemId,
        int OrderId,
        DateTime OrderCreatedAt,
        int ProductId,
        string ProductName,
        decimal Quantity,
        decimal Price,
        decimal RefundedAmount,
        int CategoryId,
        string CategoryName)
    {
        public decimal LineNet => Price * Quantity - RefundedAmount;
    }
}
