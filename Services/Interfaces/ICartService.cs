using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(string sessionId, string? district = null);
    Task AddToCartAsync(string sessionId, int productId, int quantity = 1, int? variantId = null);
    Task UpdateQuantityAsync(string sessionId, int cartItemId, int quantity);
    Task RemoveFromCartAsync(string sessionId, int cartItemId);
    Task<CartViewModel> RepriceForCheckoutAsync(string sessionId);
    Task ClearCartAsync(string sessionId);
    Task<int> GetCartCountAsync(string sessionId);
    Task<CouponApplyResult> ApplyCouponAsync(string sessionId, string couponCode);
    Task RemoveCouponAsync(string sessionId);
}
