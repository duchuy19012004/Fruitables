using Fruitables.Models;

namespace Fruitables.Services.Analytics.Sales;

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
