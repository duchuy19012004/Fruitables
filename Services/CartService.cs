using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IShippingService _shippingService;
    private readonly ICouponService _couponService;

    public CartService(IUnitOfWork unitOfWork, IShippingService shippingService, ICouponService couponService)
    {
        _unitOfWork      = unitOfWork;
        _shippingService = shippingService;
        _couponService   = couponService;
    }

    public async Task<CartViewModel> GetCartAsync(string sessionId, string? district = null)
    {
        var cart  = await GetOrCreateCartAsync(sessionId);
        var items = await _unitOfWork.CartItems.Query()
            .Where(ci => ci.CartId == cart.Id)
            .Include(ci => ci.Product)
            .ThenInclude(p => p.Images)
            .ToListAsync();

        var cartViewModel = new CartViewModel
        {
            Items = items.Select(ci => new CartItemViewModel
            {
                ProductId     = ci.ProductId,
                ProductName   = ci.Product.Name,
                ProductSlug   = ci.Product.Slug,
                ProductImage  = ci.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                                ?? ci.Product.Images.FirstOrDefault()?.ImageUrl ?? "",
                Price         = ci.Price,
                Quantity      = ci.Quantity,
                StockQuantity = ci.Product.StockQuantity
            }).ToList()
        };

        cartViewModel.Subtotal = cartViewModel.Items.Sum(i => i.Total);

        var totalKg = cartViewModel.Items.Sum(i => i.Quantity);
        cartViewModel.PackageSize = ShippingPackageCalculator.Calculate(totalKg);

        // Cart page loads without GHN address codes, so we cannot calculate a real
        // shipping fee here. Leave ShippingInfo null and ShippingFee at zero;
        // checkout (or AJAX callers) will compute shipping when GHN codes are known.
        cartViewModel.ShippingInfo = null;
        cartViewModel.ShippingFee  = 0m;

        cartViewModel.CouponCode = cart.CouponCode;
        cartViewModel.Discount   = cart.CouponDiscount;

        cartViewModel.Total = cartViewModel.Subtotal + cartViewModel.ShippingFee - cartViewModel.Discount;

        return cartViewModel;
    }

    public async Task AddToCartAsync(string sessionId, int productId, int quantity = 1)
    {
        var cart    = await GetOrCreateCartAsync(sessionId);
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null) return;

        var existingItem = await _unitOfWork.CartItems.Query()
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == productId);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            var cartItem = new CartItem
            {
                CartId    = cart.Id,
                ProductId = productId,
                Quantity  = quantity,
                Price     = product.SalePrice ?? product.Price
            };
            await _unitOfWork.CartItems.AddAsync(cartItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(string sessionId, int productId, int quantity)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var item = await _unitOfWork.CartItems.Query()
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == productId);

        if (item != null)
        {
            if (quantity <= 0)
                _unitOfWork.CartItems.Remove(item);
            else
                item.Quantity = Math.Min(quantity, item.Product.StockQuantity);

            cart.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task RemoveFromCartAsync(string sessionId, int productId)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var item = await _unitOfWork.CartItems.Query()
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == productId);

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
        var items = await _unitOfWork.CartItems.Query()
            .Where(ci => ci.CartId == cart.Id)
            .Include(ci => ci.Product)
            .ToListAsync();

        decimal subtotal  = items.Sum(ci => ci.Price * ci.Quantity);
        int     itemCount = items.Sum(ci => ci.Quantity);

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
