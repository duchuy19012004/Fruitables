using Fruitables.Models;

namespace Fruitables.ViewModels;

public class CartViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public List<CartGroupViewModel> Groups { get; set; } = new();
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
    public int? CartGroupId { get; set; }
    public int? SourceComboId { get; set; }
    public string? ComboName { get; set; }
    public int? ComboRevision { get; set; }
    public int? ComboQuantity { get; set; }
    public bool AllowCouponStacking { get; set; } = true;
    public decimal ComboDiscount { get; set; }
    public string? VariantName { get; set; }
    public string? VariantSKU { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    public decimal MinOrderQuantity { get; set; } = 1;
    public string ProductSlug { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal StockQuantity { get; set; } = decimal.MaxValue;
    public bool IsAvailable { get; set; } = true;
    public decimal Total => Price * Quantity - ComboDiscount;
}

public class CartGroupViewModel
{
    public int Id { get; set; }
    public int ComboId { get; set; }
    public int ComboRevision { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal OriginalTotal { get; set; }
    public decimal FinalTotal { get; set; }
    public decimal Discount { get; set; }
    public bool AllowCouponStacking { get; set; }
    public bool IsValid { get; set; }
    public List<CartItemViewModel> Items { get; set; } = new();
}

public sealed record CartAddItemRequest(
    int ProductId,
    decimal Quantity,
    int? ProductVariantId = null);

public sealed record CartMutationResult(
    bool Success,
    string Message)
{
    public static CartMutationResult Ok(string message = "ÄÃ£ cáº­p nháº­t giá» hÃ ng.") =>
        new(true, message);

    public static CartMutationResult Fail(string message) =>
        new(false, message);
}
