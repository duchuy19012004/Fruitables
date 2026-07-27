using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Repositories.Interfaces;

// Interface repository đơn hàng: mở rộng IRepository<Order> với các truy vấn đặc thù.
public interface IOrderRepository : IRepository<Order>
{
    // Lấy danh sách đơn hàng của 1 user với phân trang + lọc (trạng thái, ngày, search)
    Task<PagedResult<Order>> GetOrdersByUserIdAsync(int userId, OrderHistoryFilter filter);

    // Lấy đơn hàng + chi tiết (items, address, history) — kiểm tra quyền sở hữu theo userId
    Task<Order?> GetOrderWithDetailsAsync(int orderId, int userId);

    // Lấy lịch sử thay đổi trạng thái của đơn hàng
    Task<List<OrderStatusHistory>> GetOrderStatusHistoryAsync(int orderId);

    // Hủy đơn hàng + hoàn trả stock trong 1 transaction
    Task<StockRestoreResult> CancelOrderWithStockRestoreAsync(int orderId, string cancelReason, int? userId = null);
}
