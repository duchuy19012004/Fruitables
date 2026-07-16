using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace Fruitables.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICouponService _couponService;
    private readonly IProductPricingService? _pricing;

    public CartService(IUnitOfWork unitOfWork, ICouponService couponService, IProductPricingService? pricing = null)
    {
        _unitOfWork    = unitOfWork;
        _couponService = couponService;
        _pricing = pricing;
    }

    public async Task<CartViewModel> GetCartAsync(string sessionId, string? district = null)
    {
        var cart  = await GetOrCreateCartAsync(sessionId);
        var items = await _unitOfWork.CartItems.Query()
            .Where(ci => ci.CartId == cart.Id)
            .Include(ci => ci.Product)
            .ThenInclude(p => p.Images)
            .Include(ci => ci.Product)
            .ThenInclude(p => p.Variants)
            .Include(ci => ci.ProductVariant)
            .ToListAsync();
        if (await RefreshItemPricesAsync(items))
            await _unitOfWork.SaveChangesAsync();

        var cartViewModel = new CartViewModel
        {
            Items = items.Select(ci => new CartItemViewModel
            {
                CartItemId   = ci.Id,
                ProductId     = ci.ProductId,
                ProductVariantId = ci.ProductVariantId,
                VariantName = ci.ProductVariant?.Name,
                VariantSKU = ci.ProductVariant?.SKU,
                ProductName   = ci.Product.Name,
                ProductSlug   = ci.Product.Slug,
                ProductImage  = ci.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                                ?? ci.Product.Images.FirstOrDefault()?.ImageUrl ?? "",
                Price         = ci.Price,
                Quantity      = ci.Quantity,
                StockQuantity = ci.ProductVariant?.StockQuantity ?? ci.Product.StockQuantity,
                IsAvailable = ci.Product.IsActive && !ci.Product.IsDeleted &&
                    (ci.ProductVariantId.HasValue
                        ? ci.ProductVariant?.IsActive == true
                        : !ci.Product.Variants.Any(v => v.IsActive))
            }).ToList()
        };

        cartViewModel.Subtotal = cartViewModel.Items.Sum(i => i.Total);

        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await _couponService.ApplyCouponAsync(cart.CouponCode, cartViewModel.Subtotal,
                cartViewModel.Items.Sum(item => item.Quantity));
            if (coupon.Success)
                cart.CouponDiscount = coupon.DiscountAmount;
            else
            {
                cart.CouponCode = null;
                cart.CouponDiscount = 0;
            }
            await _unitOfWork.SaveChangesAsync();
        }

        // Cart page loads without GHN address codes, so we cannot calculate a real
        // shipping fee here. Leave ShippingInfo null and ShippingFee at zero;
        // checkout (or AJAX callers) will compute shipping when GHN codes are known.
        cartViewModel.ShippingInfo = null;
        cartViewModel.ShippingFee  = 0m;

        cartViewModel.CouponCode = cart.CouponCode;
        cartViewModel.Discount   = cart.CouponDiscount;

        cartViewModel.Total = cartViewModel.Subtotal + cartViewModel.ShippingFee - cartViewModel.Discount;
        var tokenSource = string.Join('|', cartViewModel.Items.OrderBy(i => i.CartItemId)
            .Select(i => $"{i.CartItemId}:{i.ProductId}:{i.ProductVariantId}:{i.Quantity}:{i.Price}:{i.StockQuantity}:{i.IsAvailable}"))
            + $"|{cartViewModel.CouponCode}:{cartViewModel.Discount}";
        cartViewModel.PricingToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenSource)));

        return cartViewModel;
    }

    public async Task AddToCartAsync(string sessionId, int productId, int quantity = 1, int? variantId = null)
    {
        var cart    = await GetOrCreateCartAsync(sessionId);
        var product = await _unitOfWork.Products.Query().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && !p.IsDeleted);
        if (product == null || quantity <= 0) return;
        var activeVariants = product.Variants.Where(v => v.IsActive).ToList();
        ProductVariant? variant = null;
        if (activeVariants.Count > 0)
        {
            variant = activeVariants.FirstOrDefault(v => v.Id == variantId);
            if (variant == null || variant.StockQuantity <= 0) return;
        }
        else if (variantId.HasValue || product.StockQuantity <= 0) return;

        var existingItem = await _unitOfWork.CartItems.Query()
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == productId && ci.ProductVariantId == variantId);

        if (existingItem != null)
        {
            var stock = variant?.StockQuantity ?? product.StockQuantity;
            existingItem.Quantity = Math.Min(existingItem.Quantity + quantity, stock);
        }
        else
        {
            var cartItem = new CartItem
            {
                CartId    = cart.Id,
                ProductId = productId,
                ProductVariantId = variantId,
                Quantity  = Math.Min(quantity, variant?.StockQuantity ?? product.StockQuantity),
                Price     = _pricing == null
                    ? (variant?.SalePrice ?? variant?.Price ?? product.SalePrice ?? product.Price)
                    : (await _pricing.GetQuoteAsync(productId, variantId))?.EffectivePrice ?? product.Price
            };
            await _unitOfWork.CartItems.AddAsync(cartItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(string sessionId, int cartItemId, int quantity)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var item = await _unitOfWork.CartItems.Query()
            .Include(ci => ci.Product)
            .Include(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.Id == cartItemId);

        if (item != null)
        {
            var isUnavailable = !item.Product.IsActive || item.Product.IsDeleted ||
                (item.ProductVariantId.HasValue && item.ProductVariant?.IsActive != true);
            var stock = item.ProductVariant?.StockQuantity ?? item.Product.StockQuantity;
            if (quantity <= 0 || isUnavailable || stock <= 0)
                _unitOfWork.CartItems.Remove(item);
            else
                item.Quantity = Math.Min(quantity, stock);

            cart.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task RemoveFromCartAsync(string sessionId, int cartItemId)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var item = await _unitOfWork.CartItems.Query()
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.Id == cartItemId);

        if (item != null)
        {
            _unitOfWork.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ClearCartAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (cart != null)
        {
            _unitOfWork.CartItems.RemoveRange(cart.Items);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<int> GetCartCountAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query()
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (cart == null) return 0;

        return await _unitOfWork.CartItems.Query()
            .Where(ci => ci.CartId == cart.Id)
            .SumAsync(ci => ci.Quantity);
    }

    public async Task<CouponApplyResult> ApplyCouponAsync(string sessionId, string couponCode)
    {
        var cart  = await GetOrCreateCartAsync(sessionId);
        var pricedCart = await GetCartAsync(sessionId);
        decimal subtotal  = pricedCart.Subtotal;
        int     itemCount = pricedCart.Items.Sum(item => item.Quantity);

        var result = await _couponService.ApplyCouponAsync(couponCode, subtotal, itemCount);

        if (result.Success)
        {
            cart.CouponCode     = result.CouponCode;
            cart.CouponDiscount = result.DiscountAmount;
            cart.UpdatedAt      = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }

        return result;
    }

    public async Task RemoveCouponAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query()
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (cart != null)
        {
            cart.CouponCode     = null;
            cart.CouponDiscount = 0;
            cart.UpdatedAt      = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<CartViewModel> RepriceForCheckoutAsync(string sessionId)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var items = await _unitOfWork.CartItems.Query().Where(i => i.CartId == cart.Id).ToListAsync();
        await RefreshItemPricesAsync(items);

        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return await GetCartAsync(sessionId);
    }

    private async Task<bool> RefreshItemPricesAsync(List<CartItem> items)
    {
        if (_pricing == null || items.Count == 0) return false;
        var targets = items.Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId)).Distinct().ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        var changed = false;
        foreach (var item in items)
        {
            if (quotes.TryGetValue(new PriceTargetKey(item.ProductId, item.ProductVariantId), out var quote) && item.Price != quote.EffectivePrice)
            {
                item.Price = quote.EffectivePrice;
                changed = true;
            }
        }
        return changed;
    }

    private async Task<Cart> GetOrCreateCartAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query()
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (cart == null)
        {
            cart = new Cart { SessionId = sessionId };
            await _unitOfWork.Carts.AddAsync(cart);
            await _unitOfWork.SaveChangesAsync();
        }

        return cart;
    }
}
