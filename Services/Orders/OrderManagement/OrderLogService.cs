using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services.Orders.OrderManagement;

public class OrderLogService : IOrderLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ILogger<OrderLogService> _logger;

    public OrderLogService(
        ApplicationDbContext context,
        ILogger<OrderLogService> logger,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _context = context;
        _logger = logger;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? new AuditLogWriter(context);
    }

    public async Task LogStatusChangeAsync(int orderId, OrderStatus oldStatus, OrderStatus newStatus, int adminId, string? notes)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId);
        if (order == null)
            return;

        var history = OrderAggregateJson.ReadHistory(order.StatusHistoryJson, _serializer);
        order.StatusHistoryJson = OrderAggregateJson.SerializeHistory(
            OrderAggregateJson.AppendHistory(history, oldStatus, newStatus, adminId, notes, DateTime.UtcNow),
            _serializer);
        await _context.SaveChangesAsync();

        await _auditLogWriter.WriteAsync(
            "StatusChanged",
            "Order",
            orderId,
            adminId,
            JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
            JsonSerializer.Serialize(new { Status = newStatus.ToString(), Notes = notes }));

        _logger.LogInformation(
            "Order {OrderId} status changed from {OldStatus} to {NewStatus} by Admin {AdminId}",
            orderId, oldStatus, newStatus, adminId);
    }

    public async Task LogPaymentStatusChangeAsync(int orderId, PaymentStatus oldStatus, PaymentStatus newStatus, int adminId, string? notes)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId);
        if (order == null)
            return;

        var message = notes ??
            $"Trạng thái thanh toán cập nhật từ {GetPaymentStatusDisplayName(oldStatus)} sang {GetPaymentStatusDisplayName(newStatus)}";
        var history = OrderAggregateJson.ReadHistory(order.StatusHistoryJson, _serializer);
        order.StatusHistoryJson = OrderAggregateJson.SerializeHistory(
            OrderAggregateJson.AppendHistory(history, order.Status, order.Status, adminId, message, DateTime.UtcNow),
            _serializer);
        await _context.SaveChangesAsync();

        await _auditLogWriter.WriteAsync(
            "PaymentStatusChanged",
            "Order",
            orderId,
            adminId,
            JsonSerializer.Serialize(new { PaymentStatus = oldStatus.ToString() }),
            JsonSerializer.Serialize(new { PaymentStatus = newStatus.ToString(), Notes = message }));

        _logger.LogInformation(
            "Order {OrderId} payment status changed from {OldStatus} to {NewStatus} by Admin {AdminId}",
            orderId, oldStatus, newStatus, adminId);
    }

    private static string GetPaymentStatusDisplayName(PaymentStatus status) => status switch
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
