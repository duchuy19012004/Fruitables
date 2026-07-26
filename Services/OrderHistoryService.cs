using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Services;

public class OrderHistoryService : IOrderHistoryService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IRealtimeNotifier? _notifier;

    public OrderHistoryService(IOrderRepository orderRepository, IRealtimeNotifier? notifier = null)
    {
        _orderRepository = orderRepository;
        _notifier = notifier;
    }

    /// <summary>
    /// Lấy danh sách lịch sử đơn hàng của khách hàng với phân trang và lọc
    /// </summary>
    public async Task<PagedResult<OrderSummaryViewModel>> GetOrderHistoryAsync(int userId, OrderHistoryFilterViewModel filter)
    {
        // Chuyển đổi từ ViewModel sang Filter object
        var repositoryFilter = new OrderHistoryFilter
        {
            SearchTerm = filter.SearchTerm?.Trim(),
            Status = filter.Status,
            Page = filter.Page,
            PageSize = filter.PageSize,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate
        };

        // Lấy dữ liệu từ repository
        var pagedOrders = await _orderRepository.GetOrdersByUserIdAsync(userId, repositoryFilter);

        // Chuyển đổi sang OrderSummaryViewModel
        var orderSummaries = pagedOrders.Items.Select(order => new OrderSummaryViewModel
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            Total = order.Total,
            ItemCount = order.Items?.Count ?? 0,
            CanCancel = CanCancelOrder(order),
            CanReview = CanReviewOrder(order)
        }).ToList();

        return new PagedResult<OrderSummaryViewModel>
        {
            Items = orderSummaries,
            TotalCount = pagedOrders.TotalCount,
            Page = pagedOrders.Page,
            PageSize = pagedOrders.PageSize
        };
    }

    /// <summary>
    /// Lấy chi tiết đơn hàng của khách hàng
    /// </summary>
    public async Task<OrderDetailViewModel?> GetOrderDetailAsync(int orderId, int userId)
    {
        var order = await _orderRepository.GetOrderWithDetailsAsync(orderId, userId);
        if (order == null)
        {
            return null;
        }

        // Lấy lịch sử trạng thái
        var statusHistory = await _orderRepository.GetOrderStatusHistoryAsync(orderId);

        // Tìm ngày giao hàng từ lịch sử trạng thái
        var deliveredHistory = statusHistory.FirstOrDefault(sh => sh.NewStatus == OrderStatus.Delivered);

        return new OrderDetailViewModel
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            PaymentMethod = order.PaymentMethod,
            ShippingAddress = order.Address,
            ShippingMethod = order.ShippingMethod,
            Subtotal = order.Subtotal,
            ShippingFee = order.ShippingFee,
            Discount = order.Discount,
            Total = order.Total,
            Items = order.Items?.Select(item => new OrderItemViewModel
            {
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Unknown Product",
                ProductImage = item.Product?.Images?.FirstOrDefault()?.ImageUrl,
                Quantity = item.Quantity,
                Price = item.Price,
                Total = item.Total,
                SourceComboId = item.SourceComboId,
                ComboName = item.ComboNameSnapshot,
                ComboDiscount = item.ComboDiscount
            }).ToList() ?? new List<OrderItemViewModel>(),
            StatusHistory = statusHistory.Select(sh => new OrderStatusHistoryViewModel
            {
                OldStatus = sh.OldStatus,
                NewStatus = sh.NewStatus,
                CreatedAt = sh.CreatedAt,
                Notes = sh.Notes,
                AdminName = sh.Admin?.Name ?? "System"
            }).ToList(),
            CanCancel = CanCancelOrder(order),
            CanReview = CanReviewOrder(order),
            CancelReason = order.CancelReason,
            Notes = order.Notes,
            DeliveredAt = deliveredHistory?.CreatedAt
        };
    }

    /// <summary>
    /// Kiểm tra xem khách hàng có thể hủy đơn hàng không
    /// </summary>
    public async Task<bool> CanCancelOrderAsync(int orderId, int userId)
    {
        var order = await _orderRepository.GetOrderWithDetailsAsync(orderId, userId);
        return order != null && CanCancelOrder(order);
    }

    /// <summary>
    /// Hủy đơn hàng của khách hàng và hoàn trả stock
    /// </summary>
    public async Task<bool> CancelOrderAsync(int orderId, int userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        // Kiểm tra quyền và khả năng hủy đơn
        if (!await CanCancelOrderAsync(orderId, userId))
        {
            return false;
        }

        // Hủy đơn hàng và hoàn trả stock trong một transaction
        var result = await _orderRepository.CancelOrderWithStockRestoreAsync(orderId, reason.Trim(), userId);
        if (result.IsSuccess && _notifier != null)
        {
            foreach (var item in result.RestoredItems)
            {
                if (item.ProductVariantId.HasValue)
                    await _notifier.NotifyStockChangedAsync(item.ProductId, item.CurrentStock, item.ProductVariantId);
                else
                    await _notifier.NotifyStockChangedAsync(item.ProductId, item.CurrentStock);
            }
        }
        return result.IsSuccess;
    }

    /// <summary>
    /// Lấy lịch sử thay đổi trạng thái của đơn hàng
    /// </summary>
    public async Task<List<OrderStatusHistoryViewModel>> GetOrderStatusHistoryAsync(int orderId)
    {
        var statusHistory = await _orderRepository.GetOrderStatusHistoryAsync(orderId);
        
        return statusHistory.Select(sh => new OrderStatusHistoryViewModel
        {
            OldStatus = sh.OldStatus,
            NewStatus = sh.NewStatus,
            CreatedAt = sh.CreatedAt,
            Notes = sh.Notes,
            AdminName = sh.Admin?.Name ?? "System"
        }).ToList();
    }

    /// <summary>
    /// Kiểm tra xem đơn hàng có thể hủy không (business logic)
    /// </summary>
    private static bool CanCancelOrder(Order order)
    {
        // Chỉ có thể hủy đơn hàng ở trạng thái "Chờ xử lý"
        return order.Status == OrderStatus.Pending;
    }

    /// <summary>
    /// Kiểm tra xem đơn hàng có thể đánh giá không (business logic)
    /// </summary>
    private static bool CanReviewOrder(Order order)
    {
        // Chỉ có thể đánh giá đơn hàng ở trạng thái "Đã giao"
        return order.Status == OrderStatus.Delivered;
    }
}
