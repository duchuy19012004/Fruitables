namespace Fruitables.Models;

public class OrderResult
{
    public bool Success { get; set; }
    public Order? Order { get; set; }
    public OrderErrorType? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public List<InsufficientStockItem>? InsufficientStockItems { get; set; }

    public static OrderResult Ok(Order order) => new() { Success = true, Order = order };

    public static OrderResult Fail(OrderErrorType errorType, string message) =>
        new() { Success = false, ErrorType = errorType, ErrorMessage = message };

    public static OrderResult FailWithInsufficientStock(List<InsufficientStockItem> items) =>
        new()
        {
            Success = false,
            ErrorType = OrderErrorType.InsufficientStock,
            ErrorMessage = "Không đủ tồn kho để khôi phục đơn hàng",
            InsufficientStockItems = items
        };
}

public class InsufficientStockItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
}

public enum OrderErrorType
{
    NotFound,
    InvalidStatusTransition,
    InvalidPaymentStatusTransition,
    InsufficientStock,
    ConcurrencyConflict,
    ValidationError
}
