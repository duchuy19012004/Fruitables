using Fruitables.Models;
using Fruitables.ViewModels;

namespace Fruitables.Services.Orders.OrderManagement;

public interface IOrderHistoryService
{
    /// <summary>
    /// Lấy danh sách lịch sử đơn hàng của khách hàng với phân trang và lọc
    /// </summary>
    /// <param name="userId">ID của khách hàng</param>
    /// <param name="filter">Bộ lọc và tham số phân trang</param>
    /// <returns>Kết quả phân trang chứa danh sách đơn hàng</returns>
    Task<PagedResult<OrderSummaryViewModel>> GetOrderHistoryAsync(int userId, OrderHistoryFilterViewModel filter);

    /// <summary>
    /// Lấy chi tiết đơn hàng của khách hàng
    /// </summary>
    /// <param name="orderId">ID đơn hàng</param>
    /// <param name="userId">ID khách hàng (để kiểm tra quyền truy cập)</param>
    /// <returns>Chi tiết đơn hàng hoặc null nếu không tìm thấy/không có quyền</returns>
    Task<OrderDetailViewModel?> GetOrderDetailAsync(int orderId, int userId);

    /// <summary>
    /// Kiểm tra xem khách hàng có thể hủy đơn hàng không
    /// </summary>
    /// <param name="orderId">ID đơn hàng</param>
    /// <param name="userId">ID khách hàng</param>
    /// <returns>True nếu có thể hủy, False nếu không thể</returns>
    Task<bool> CanCancelOrderAsync(int orderId, int userId);

    /// <summary>
    /// Hủy đơn hàng của khách hàng
    /// </summary>
    /// <param name="orderId">ID đơn hàng</param>
    /// <param name="userId">ID khách hàng</param>
    /// <param name="reason">Lý do hủy đơn hàng</param>
    /// <returns>True nếu hủy thành công, False nếu thất bại</returns>
    Task<bool> CancelOrderAsync(int orderId, int userId, string reason);

    /// <summary>
    /// Lấy lịch sử thay đổi trạng thái của đơn hàng
    /// </summary>
    /// <param name="orderId">ID đơn hàng</param>
    /// <returns>Danh sách lịch sử thay đổi trạng thái</returns>
    Task<List<OrderStatusHistoryViewModel>> GetOrderStatusHistoryAsync(int orderId);
}
