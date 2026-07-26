using Fruitables.ViewModels;

namespace Fruitables.Services.Interfaces;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(string sessionId, string? district = null);
    Task<CartMutationResult> AddToCartAsync(
        string sessionId,
        int productId,
        int quantity = 1,
        int? variantId = null);
    Task<CartMutationResult> AddItemsToCartAsync(
        string sessionId,
        IReadOnlyCollection<CartAddItemRequest> items);
    Task<CartMutationResult> AddComboToCartAsync(string sessionId, int comboId);
    Task<CartMutationResult> UpdateComboQuantityAsync(string sessionId, int cartGroupId, int quantity);
    Task RemoveComboAsync(string sessionId, int cartGroupId);
    Task UpdateQuantityAsync(string sessionId, int cartItemId, int quantity);
    Task RemoveFromCartAsync(string sessionId, int cartItemId);
    Task<CartViewModel> RepriceForCheckoutAsync(string sessionId);
    Task ClearCartAsync(string sessionId);
    Task<int> GetCartCountAsync(string sessionId);
    Task<CouponApplyResult> ApplyCouponAsync(string sessionId, string couponCode);
    Task RemoveCouponAsync(string sessionId);
}
