using Microsoft.Extensions.Logging;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Communications;

namespace Fruitables.Services.Orders.OrderManagement;

public class OrderLogService : IOrderLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderLogService> _logger;

    public OrderLogService(ApplicationDbContext context, ILogger<OrderLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogStatusChangeAsync(int orderId, OrderStatus oldStatus, OrderStatus newStatus, int adminId, string? notes)
    {
        var history = new OrderStatusHistory
        {
            OrderId = orderId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            AdminId = adminId,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderStatusHistories.Add(history);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Order {OrderId} status changed from {OldStatus} to {NewStatus} by Admin {AdminId}",
            orderId, oldStatus, newStatus, adminId);
    }

    public async Task LogPaymentStatusChangeAsync(int orderId, PaymentStatus oldStatus, PaymentStatus newStatus, int adminId, string? notes)
    {
        // For payment status changes, we also create an OrderStatusHistory record
        // with the current order status (since OrderStatusHistory tracks order status, not payment status)
        // We use the Notes field to indicate this is a payment status change
        var order = await _context.Orders.FindAsync(orderId);
        if (order != null)
        {
            var history = new OrderStatusHistory
            {
                OrderId = orderId,
                OldStatus = order.Status,
                NewStatus = order.Status,
                AdminId = adminId,
                Notes = notes ?? $"Trạng thái thanh toán cập nhật từ {GetPaymentStatusDisplayName(oldStatus)} sang {GetPaymentStatusDisplayName(newStatus)}",
                CreatedAt = DateTime.UtcNow
            };

            _context.OrderStatusHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Order {OrderId} payment status changed from {OldStatus} to {NewStatus} by Admin {AdminId}",
            orderId, oldStatus, newStatus, adminId);
    }

    private string GetPaymentStatusDisplayName(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Chờ thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        _ => status.ToString()
    };

    public Task LogErrorAsync(string action, int? orderId, Exception ex)
    {
        _logger.LogError(ex, "Error in {Action} for Order {OrderId}: {Message}", action, orderId, ex.Message);
        return Task.CompletedTask;
    }
}
