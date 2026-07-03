using Fruitables.Models;

namespace Fruitables.ViewModels;

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public decimal Discount { get; set; }
    public string? CouponMessage { get; set; }
    
    public ShippingInfo? ShippingInfo { get; set; }
    public PackageSize? PackageSize { get; set; }
}

public class CartItemViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int StockQuantity { get; set; }
    public decimal Total => Price * Quantity;
}
