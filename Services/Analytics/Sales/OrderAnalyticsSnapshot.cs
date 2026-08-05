using Fruitables.Models;

namespace Fruitables.Services.Analytics.Sales;

public readonly record struct OrderAnalyticsSnapshot(
    decimal Total,
    PaymentStatus PaymentStatus,
    OrderStatus Status,
    decimal Discount,
    decimal ShippingFee,
    decimal Subtotal,
    decimal SuccessfulRefundAmount = 0m);
