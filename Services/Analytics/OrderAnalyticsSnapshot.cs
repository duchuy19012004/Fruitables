using Fruitables.Models;

namespace Fruitables.Services.Analytics;

public readonly record struct OrderAnalyticsSnapshot(
    decimal Total,
    PaymentStatus PaymentStatus,
    OrderStatus Status,
    decimal Discount,
    decimal ShippingFee,
    decimal Subtotal);
