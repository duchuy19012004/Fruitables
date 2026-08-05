using System.ComponentModel.DataAnnotations;
using Fruitables.Models;

namespace Fruitables.ViewModels;

/// <summary>
/// ViewModel cho bộ lọc và phân trang lịch sử đơn hàng
/// </summary>
public class OrderHistoryFilterViewModel
{
    /// <summary>
    /// Từ khóa tìm kiếm (mã đơn hàng)
    /// </summary>
    [MaxLength(50)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Lọc theo trạng thái đơn hàng
    /// </summary>
    public OrderStatus? Status { get; set; }

    /// <summary>
    /// Số trang hiện tại (bắt đầu từ 1)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Số lượng đơn hàng mỗi trang
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Lọc từ ngày
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Lọc đến ngày
    /// </summary>
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Filter object cho Repository layer (không có validation attributes)
/// </summary>
public class OrderHistoryFilter
{
    public string? SearchTerm { get; set; }
    public OrderStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// ViewModel cho tóm tắt đơn hàng trong danh sách
/// </summary>
public class OrderSummaryViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReview { get; set; }
}

/// <summary>
/// ViewModel cho chi tiết đơn hàng
/// </summary>
public class OrderDetailViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    // Thông tin giao hàng
    public Address? ShippingAddress { get; set; }
    public ShippingMethod ShippingMethod { get; set; }

    // Thông tin tài chính
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    // Sản phẩm
    public List<OrderItemViewModel> Items { get; set; } = new();

    // Lịch sử trạng thái
    public List<OrderStatusHistoryViewModel> StatusHistory { get; set; } = new();

    // Hành động
    public bool CanCancel { get; set; }
    public bool CanReview { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }

    // Thông tin đặc biệt theo trạng thái
    /// <summary>
    /// Ngày giao hàng (chỉ có khi Status = Delivered)
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Mã theo dõi vận chuyển (chỉ có khi Status = Shipped)
    /// </summary>
    public string? TrackingNumber { get; set; }
}

/// <summary>
/// ViewModel cho sản phẩm trong đơn hàng
/// </summary>
public class OrderItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public int? SourceComboId { get; set; }
    public string? ComboName { get; set; }
    public decimal ComboDiscount { get; set; }
}

/// <summary>
/// ViewModel cho lịch sử thay đổi trạng thái đơn hàng
/// </summary>
public class OrderStatusHistoryViewModel
{
    public OrderStatus OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public string AdminName { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả phân trang generic
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của items</typeparam>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    
    // Aliases for compatibility
    public List<T> Data => Items;
    public int CurrentPage => Page;
}