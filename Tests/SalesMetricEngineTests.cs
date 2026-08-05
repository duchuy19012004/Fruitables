using Fruitables.Models;
using Fruitables.Services.Analytics.Sales;
using Xunit;

namespace Fruitables.Tests;

public class SalesMetricEngineTests
{
    private static OrderAnalyticsSnapshot O(decimal total, PaymentStatus pay, OrderStatus st) =>
        new(total, pay, st, 0, 0, total);

    [Fact]
    public void Gross_SumsPaidOnly()
    {
        var orders = new[]
        {
            O(100, PaymentStatus.Paid, OrderStatus.Delivered),
            O(50, PaymentStatus.Paid, OrderStatus.Processing),
            O(80, PaymentStatus.Pending, OrderStatus.Pending),
            O(20, PaymentStatus.Refunded, OrderStatus.Cancelled),
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
            O(30, PaymentStatus.Refunded, OrderStatus.Cancelled),
            O(50, PaymentStatus.Paid, OrderStatus.Processing),
        };
        Assert.Equal(110, SalesMetricEngine.Net(orders)); // 140 - 30
    }

    [Fact]
    public void Net_subtracts_successful_partial_refunds_from_paid_orders()
    {
        var orders = new[]
        {
            new OrderAnalyticsSnapshot(100m, PaymentStatus.Paid, OrderStatus.Delivered, 0m, 0m, 100m)
            {
                SuccessfulRefundAmount = 15m
            }
        };

        Assert.Equal(85m, SalesMetricEngine.Net(orders));
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
            O(1, PaymentStatus.Refunded, OrderStatus.Cancelled),
        };
        // refund count / paid count = 1/2 = 50
        Assert.Equal(50m, SalesMetricEngine.RefundRatePercent(orders));
    }
}
