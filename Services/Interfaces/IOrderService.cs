using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CheckoutViewModel model, string sessionId, int? userId = null);
    Task<Order?> GetOrderByIdAsync(int id);
    Task<Order?> GetOrderByNumberAsync(string orderNumber);
    Task<List<Order>> GetOrdersByUserIdAsync(int userId);

    /// <summary>
    /// Gets the shipping address from order snapshot
    /// </summary>
    /// <param name="orderId">The order ID</param>
    /// <returns>Address from snapshot or null if not found/invalid</returns>
    Task<Address?> GetShippingAddressFromSnapshotAsync(int orderId);
}
