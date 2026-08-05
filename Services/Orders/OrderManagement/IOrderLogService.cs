using Fruitables.Models;

namespace Fruitables.Services.Orders.OrderManagement;

public interface IOrderLogService
{
    Task LogStatusChangeAsync(int orderId, OrderStatus oldStatus, OrderStatus newStatus, int adminId, string? notes);
    Task LogPaymentStatusChangeAsync(int orderId, PaymentStatus oldStatus, PaymentStatus newStatus, int adminId, string? notes);
    Task LogErrorAsync(string action, int? orderId, Exception ex);
}
