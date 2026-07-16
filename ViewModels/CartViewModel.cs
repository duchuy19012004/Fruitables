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
    public string PricingToken { get; set; } = string.Empty;
    public string? CouponMessage { get; set; }
    
    public ShippingInfo? ShippingInfo { get; set; }

    public ShippingPackage ShippingPackage =>
        ShippingPackage.FromTotalKg(Items?.Sum(i => i.Quantity) ?? 0);
}

public class CartItemViewModel
{
    public int CartItemId { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? VariantName { get; set; }
    public string? VariantSKU { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int StockQuantity { get; set; } = int.MaxValue;
    public bool IsAvailable { get; set; } = true;
    public decimal Total => Price * Quantity;
}
