namespace Fruitables.ViewModels;

/// <summary>
/// Kết quả của việc hoàn trả stock khi hủy đơn hàng
/// </summary>
public class StockRestoreResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<StockRestoreItem> RestoredItems { get; set; } = new();

    public static StockRestoreResult Success(List<StockRestoreItem> items)
        => new() { IsSuccess = true, RestoredItems = items };

    public static StockRestoreResult Fail(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}

/// <summary>
/// Thông tin chi tiết về stock đã hoàn trả cho một sản phẩm
/// </summary>
public class StockRestoreItem
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityRestored { get; set; }
    public decimal CurrentStock { get; set; }
}
