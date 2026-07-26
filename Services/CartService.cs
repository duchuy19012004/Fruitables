using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Fruitables.Services.Pricing;
using System.Security.Cryptography;
using System.Text;

namespace Fruitables.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICouponService _couponService;
    private readonly IProductPricingService _pricing;
    private readonly TimeProvider _timeProvider;

    public CartService(IUnitOfWork unitOfWork, ICouponService couponService, IProductPricingService pricing, TimeProvider? timeProvider = null)
    {
        _unitOfWork    = unitOfWork;
        _couponService = couponService;
        _pricing = pricing;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            .Include(ci => ci.CartGroup)
            .ThenInclude(group => group!.Combo)
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
                CartGroupId = ci.CartGroupId,
                SourceComboId = ci.CartGroup?.ComboId,
                ComboName = ci.CartGroup?.ComboName,
                ComboRevision = ci.CartGroup?.ComboRevision,
                ComboQuantity = ci.CartGroup?.Quantity,
                AllowCouponStacking = ci.CartGroup?.AllowCouponStacking ?? true,
                ComboDiscount = ci.ComboDiscount,
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
                        : !ci.Product.Variants.Any(v => v.IsActive)) &&
                    (ci.CartGroup == null ||
                        (ci.CartGroup.Combo.IsAvailableAt(_timeProvider.GetUtcNow()) && ci.CartGroup.ComboRevision == ci.CartGroup.Combo.Revision))
            }).ToList()
        };

        cartViewModel.Groups = items
            .Where(item => item.CartGroup != null)
            .GroupBy(item => item.CartGroup!)
            .Select(group => new CartGroupViewModel
            {
                Id = group.Key.Id,
                ComboId = group.Key.ComboId,
                ComboRevision = group.Key.ComboRevision,
                ComboName = group.Key.ComboName,
                Quantity = group.Key.Quantity,
                OriginalTotal = group.Key.OriginalTotal,
                FinalTotal = group.Key.FinalTotal,
                Discount = group.Key.Discount,
                AllowCouponStacking = group.Key.AllowCouponStacking,
                IsValid = group.Key.Combo.IsAvailableAt(_timeProvider.GetUtcNow()) && group.Key.ComboRevision == group.Key.Combo.Revision &&
                    group.All(item => item.Product.IsActive && !item.Product.IsDeleted),
                Items = cartViewModel.Items.Where(item => item.CartGroupId == group.Key.Id).ToList()
            })
            .ToList();
        cartViewModel.Subtotal = cartViewModel.Items.Sum(i => i.Total);

        var couponEligibleItems = cartViewModel.Items.Where(item => item.AllowCouponStacking).ToList();
        var couponEligibleSubtotal = couponEligibleItems.Sum(item => item.Total);
        var couponEligibleCount = couponEligibleItems.Sum(item => item.Quantity);
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await _couponService.ApplyCouponAsync(cart.CouponCode, couponEligibleSubtotal,
                couponEligibleCount);
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
            .Select(i => $"{i.CartItemId}:{i.ProductId}:{i.ProductVariantId}:{i.CartGroupId}:{i.ComboRevision}:{i.Quantity}:{i.Price}:{i.ComboDiscount}:{i.StockQuantity}:{i.IsAvailable}"))
            + $"|{cartViewModel.CouponCode}:{cartViewModel.Discount}";
        cartViewModel.PricingToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenSource)));

        return cartViewModel;
    }

    public async Task<CartMutationResult> AddToCartAsync(
        string sessionId,
        int productId,
        int quantity = 1,
        int? variantId = null)
    {
        if (quantity <= 0)
            return CartMutationResult.Fail("Sá»‘ lÆ°á»£ng pháº£i lá»›n hÆ¡n 0.");

        var cart = await GetOrCreateCartAsync(sessionId);
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p =>
                p.Id == productId &&
                p.IsActive &&
                !p.IsDeleted);

        if (product == null)
            return CartMutationResult.Fail("Sáº£n pháº©m khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng bÃ¡n.");

        var activeVariants = product.Variants
            .Where(variant => variant.IsActive)
            .ToList();

        ProductVariant? variant = null;

        if (activeVariants.Count > 0)
        {
            variant = activeVariants.FirstOrDefault(item => item.Id == variantId);
            if (variant == null)
                return CartMutationResult.Fail("Vui lÃ²ng chá»n má»™t biáº¿n thá»ƒ Ä‘ang bÃ¡n.");

            if (variant.StockQuantity <= 0)
                return CartMutationResult.Fail("Biáº¿n thá»ƒ Ä‘Ã£ háº¿t hÃ ng.");
        }
        else
        {
            if (variantId.HasValue)
                return CartMutationResult.Fail("Biáº¿n thá»ƒ khÃ´ng há»£p lá»‡.");

            if (product.StockQuantity <= 0)
                return CartMutationResult.Fail("Sáº£n pháº©m Ä‘Ã£ háº¿t hÃ ng.");
        }

        var quote = await _pricing.GetQuoteAsync(productId, variantId);
        if (quote == null)
        {
            return CartMutationResult.Fail(
                "KhÃ´ng thá»ƒ xÃ¡c Ä‘á»‹nh giÃ¡ hiá»‡n táº¡i cá»§a sáº£n pháº©m. Vui lÃ²ng táº£i láº¡i trang.");
        }

        var existingItem = await _unitOfWork.CartItems.Query()
            .FirstOrDefaultAsync(item =>
                item.CartId == cart.Id &&
                item.CartGroupId == null &&
                item.ProductId == productId &&
                item.ProductVariantId == variantId);

        var stock = variant?.StockQuantity ?? product.StockQuantity;

        if (existingItem != null)
        {
            existingItem.Quantity = Math.Min(
                existingItem.Quantity + quantity,
                stock);

            existingItem.Price = quote.EffectivePrice;
        }
        else
        {
            await _unitOfWork.CartItems.AddAsync(new CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                ProductVariantId = variantId,
                Quantity = Math.Min(quantity, stock),
                Price = quote.EffectivePrice
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return CartMutationResult.Ok("ÄÃ£ thÃªm sáº£n pháº©m vÃ o giá» hÃ ng.");
    }

    public async Task<CartMutationResult> AddItemsToCartAsync(
        string sessionId,
        IReadOnlyCollection<CartAddItemRequest> items)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || items.Count == 0)
            return CartMutationResult.Fail("Danh sách sản phẩm không hợp lệ.");

        var groupedItems = items
            .GroupBy(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .Select(group => new CartAddItemRequest(
                group.Key.ProductId,
                group.Sum(item => item.Quantity),
                group.Key.ProductVariantId))
            .ToList();

        if (groupedItems.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
            return CartMutationResult.Fail("Sản phẩm hoặc số lượng không hợp lệ.");

        var productIds = groupedItems.Select(item => item.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.Query()
            .Where(product => productIds.Contains(product.Id) && product.IsActive && !product.IsDeleted)
            .Include(product => product.Variants)
            .ToDictionaryAsync(product => product.Id);

        if (products.Count != productIds.Count)
            return CartMutationResult.Fail("Một số sản phẩm không còn được bán.");

        foreach (var request in groupedItems)
        {
            var product = products[request.ProductId];
            var activeVariants = product.Variants.Where(variant => variant.IsActive).ToList();
            var variant = request.ProductVariantId.HasValue
                ? activeVariants.FirstOrDefault(item => item.Id == request.ProductVariantId.Value)
                : null;

            if (activeVariants.Count > 0 && variant == null)
                return CartMutationResult.Fail($"Vui lòng chọn biến thể hợp lệ cho '{product.Name}'.");
            if (activeVariants.Count == 0 && request.ProductVariantId.HasValue)
                return CartMutationResult.Fail($"Biến thể của '{product.Name}' không hợp lệ.");

            var stock = variant?.StockQuantity ?? product.StockQuantity;
            if (stock < request.Quantity)
                return CartMutationResult.Fail($"'{product.Name}' không đủ tồn kho.");
        }

        var targets = groupedItems
            .Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        if (targets.Any(target => !quotes.ContainsKey(target)))
            return CartMutationResult.Fail("Không thể xác định giá hiện tại của một số sản phẩm.");

        var cart = await _unitOfWork.Carts.Query()
            .FirstOrDefaultAsync(item => item.SessionId == sessionId);
        var existingItems = cart == null
            ? new List<CartItem>()
            : await _unitOfWork.CartItems.Query()
                .Where(item => item.CartId == cart.Id && item.CartGroupId == null && productIds.Contains(item.ProductId))
                .ToListAsync();

        foreach (var request in groupedItems)
        {
            var product = products[request.ProductId];
            var variant = request.ProductVariantId.HasValue
                ? product.Variants.First(item => item.Id == request.ProductVariantId.Value)
                : null;
            var existing = existingItems.FirstOrDefault(item =>
                item.ProductId == request.ProductId &&
                item.ProductVariantId == request.ProductVariantId);
            var stock = variant?.StockQuantity ?? product.StockQuantity;

            if ((existing?.Quantity ?? 0) + request.Quantity > stock)
                return CartMutationResult.Fail($"'{product.Name}' không đủ tồn kho cho số lượng trong giỏ.");
        }

        if (cart == null)
        {
            cart = new Cart { SessionId = sessionId };
            await _unitOfWork.Carts.AddAsync(cart);
        }

        foreach (var request in groupedItems)
        {
            var key = new PriceTargetKey(request.ProductId, request.ProductVariantId);
            var existing = existingItems.FirstOrDefault(item =>
                item.ProductId == request.ProductId &&
                item.ProductVariantId == request.ProductVariantId);

            if (existing != null)
            {
                existing.Quantity += request.Quantity;
                existing.Price = quotes[key].EffectivePrice;
            }
            else
            {
                await _unitOfWork.CartItems.AddAsync(new CartItem
                {
                    Cart = cart,
                    ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId,
                    Quantity = request.Quantity,
                    Price = quotes[key].EffectivePrice
                });
            }
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return CartMutationResult.Ok("Đã thêm toàn bộ combo vào giỏ hàng.");
    }

    public async Task<CartMutationResult> AddComboToCartAsync(string sessionId, int comboId)
    {
        var combo = await _unitOfWork.Combos.Query()
            .Where(item => item.Id == comboId && item.IsActive)
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Variants)
            .Include(item => item.Items)
                .ThenInclude(item => item.ProductVariant)
            .FirstOrDefaultAsync();
        if (combo == null || !combo.IsAvailableAt(_timeProvider.GetUtcNow()) || combo.Items.Count < 2)
            return CartMutationResult.Fail("Combo không tồn tại, chưa đến lịch bán hoặc đã ngừng bán.");

        var targets = combo.Items
            .Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        if (targets.Any(target => !quotes.ContainsKey(target)))
            return CartMutationResult.Fail("Một số sản phẩm trong combo không còn được bán.");

        foreach (var item in combo.Items)
        {
            var stock = item.ProductVariant?.StockQuantity ?? item.Product.StockQuantity;
            if (!item.Product.IsActive || item.Product.IsDeleted ||
                (item.ProductVariantId.HasValue && item.ProductVariant?.IsActive != true) ||
                stock < item.Quantity)
                return CartMutationResult.Fail($"'{item.Product.Name}' không đủ điều kiện bán trong combo.");
        }

        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(item => item.SessionId == sessionId);
        var existingCartItems = cart == null
            ? new List<CartItem>()
            : await _unitOfWork.CartItems.Query().Where(item => item.CartId == cart.Id).ToListAsync();

        foreach (var comboItem in combo.Items)
        {
            var alreadyInCart = existingCartItems
                .Where(item => item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId)
                .Sum(item => item.Quantity);
            var stock = comboItem.ProductVariant?.StockQuantity ?? comboItem.Product.StockQuantity;
            if (alreadyInCart + comboItem.Quantity > stock)
                return CartMutationResult.Fail($"'{comboItem.Product.Name}' không đủ tồn kho cho combo.");
        }

        if (cart == null)
        {
            cart = new Cart { SessionId = sessionId };
            await _unitOfWork.Carts.AddAsync(cart);
        }

        var group = cart.Id == 0
            ? null
            : await _unitOfWork.CartGroups.Query()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.CartId == cart.Id && item.ComboId == combo.Id && item.ComboRevision == combo.Revision);

        if (group == null)
        {
            group = new CartGroup
            {
                Cart = cart,
                ComboId = combo.Id,
                ComboRevision = combo.Revision,
                ComboName = combo.Name,
                Quantity = 1,
                AllowCouponStacking = combo.AllowCouponStacking,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            await _unitOfWork.CartGroups.AddAsync(group);
            foreach (var comboItem in combo.Items.OrderBy(item => item.SortOrder))
            {
                var key = new PriceTargetKey(comboItem.ProductId, comboItem.ProductVariantId);
                var cartItem = new CartItem
                {
                    Cart = cart,
                    CartGroup = group,
                    ProductId = comboItem.ProductId,
                    ProductVariantId = comboItem.ProductVariantId,
                    Quantity = comboItem.Quantity,
                    Price = quotes[key].EffectivePrice
                };
                group.Items.Add(cartItem);
                await _unitOfWork.CartItems.AddAsync(cartItem);
            }
        }
        else
        {
            group.Quantity++;
            foreach (var comboItem in combo.Items)
            {
                var cartItem = group.Items.First(item =>
                    item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId);
                cartItem.Quantity += comboItem.Quantity;
            }
        }

        ApplyComboPricing(group, combo, quotes);
        group.UpdatedAt = DateTime.UtcNow;
        cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return CartMutationResult.Ok($"Đã thêm combo '{combo.Name}' vào giỏ hàng.");
    }

    public async Task<CartMutationResult> UpdateComboQuantityAsync(string sessionId, int cartGroupId, int quantity)
    {
        var group = await _unitOfWork.CartGroups.Query()
            .Include(item => item.Cart)
            .Include(item => item.Items)
            .Include(item => item.Combo)
                .ThenInclude(combo => combo.Items)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.Variants)
            .Include(item => item.Combo)
                .ThenInclude(combo => combo.Items)
                    .ThenInclude(item => item.ProductVariant)
            .FirstOrDefaultAsync(item => item.Id == cartGroupId && item.Cart.SessionId == sessionId);
        if (group == null) return CartMutationResult.Fail("Không tìm thấy combo trong giỏ hàng.");

        if (quantity <= 0)
        {
            _unitOfWork.CartItems.RemoveRange(group.Items);
            _unitOfWork.CartGroups.Remove(group);
            await _unitOfWork.SaveChangesAsync();
            return CartMutationResult.Ok("Đã xóa combo khỏi giỏ hàng.");
        }

        var combo = group.Combo;
        if (!combo.IsAvailableAt(_timeProvider.GetUtcNow()) || combo.Revision != group.ComboRevision)
            return CartMutationResult.Fail("Combo đã thay đổi hoặc ngừng bán. Vui lòng xóa và thêm lại.");
        var otherItems = await _unitOfWork.CartItems.Query()
            .Where(item => item.CartId == group.CartId && item.CartGroupId != group.Id)
            .ToListAsync();
        foreach (var comboItem in combo.Items)
        {
            var desired = comboItem.Quantity * quantity;
            var otherQuantity = otherItems
                .Where(item => item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId)
                .Sum(item => item.Quantity);
            var stock = comboItem.ProductVariant?.StockQuantity ?? comboItem.Product.StockQuantity;
            if (desired + otherQuantity > stock)
                return CartMutationResult.Fail($"'{comboItem.Product.Name}' không đủ tồn kho.");
        }

        var targets = combo.Items.Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId)).ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        if (targets.Any(target => !quotes.ContainsKey(target)))
            return CartMutationResult.Fail("Không thể xác định giá hiện tại của combo.");

        group.Quantity = quantity;
        foreach (var comboItem in combo.Items)
        {
            var cartItem = group.Items.First(item =>
                item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId);
            cartItem.Quantity = comboItem.Quantity * quantity;
            cartItem.Price = quotes[new PriceTargetKey(comboItem.ProductId, comboItem.ProductVariantId)].EffectivePrice;
        }
        ApplyComboPricing(group, combo, quotes);
        group.UpdatedAt = DateTime.UtcNow;
        group.Cart.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();
        return CartMutationResult.Ok("Đã cập nhật số lượng combo.");
    }

    public async Task RemoveComboAsync(string sessionId, int cartGroupId)
    {
        var group = await _unitOfWork.CartGroups.Query()
            .Include(item => item.Cart)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == cartGroupId && item.Cart.SessionId == sessionId);
        if (group == null) return;

        _unitOfWork.CartItems.RemoveRange(group.Items);
        _unitOfWork.CartGroups.Remove(group);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(string sessionId, int cartItemId, int quantity)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var item = await _unitOfWork.CartItems.Query()
            .Include(ci => ci.Product)
            .Include(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.Id == cartItemId);

        if (item != null && item.CartGroupId == null)
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

        if (item != null && item.CartGroupId == null)
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
            var groups = await _unitOfWork.CartGroups.Query().Where(group => group.CartId == cart.Id).ToListAsync();
            _unitOfWork.CartGroups.RemoveRange(groups);
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
        var eligibleItems = pricedCart.Items.Where(item => item.AllowCouponStacking).ToList();
        decimal subtotal = eligibleItems.Sum(item => item.Total);
        int itemCount = eligibleItems.Sum(item => item.Quantity);

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

    public Task<CartViewModel> RepriceForCheckoutAsync(string sessionId) => GetCartAsync(sessionId);

    private async Task<bool> RefreshItemPricesAsync(List<CartItem> items)
    {
        if (items.Count == 0) return false;
        var targets = items.Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId)).Distinct().ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        var changed = false;

        foreach (var item in items.Where(item => item.CartGroupId == null))
        {
            if (!quotes.TryGetValue(new PriceTargetKey(item.ProductId, item.ProductVariantId), out var quote))
                continue;
            if (item.Price != quote.EffectivePrice || item.ComboDiscount != 0)
            {
                item.Price = quote.EffectivePrice;
                item.ComboDiscount = 0;
                changed = true;
            }
        }

        foreach (var group in items
            .Where(item => item.CartGroup != null)
            .GroupBy(item => item.CartGroup!)
            .Select(itemsByGroup => itemsByGroup.Key))
        {
            if (!group.Combo.IsAvailableAt(_timeProvider.GetUtcNow()) || group.ComboRevision != group.Combo.Revision ||
                group.Items.Any(item => !quotes.ContainsKey(new PriceTargetKey(item.ProductId, item.ProductVariantId))))
                continue;

            var oldOriginal = group.OriginalTotal;
            var oldFinal = group.FinalTotal;
            var oldDiscount = group.Discount;
            var oldItemValues = group.Items.Select(item => (item.Id, item.Price, item.ComboDiscount)).ToList();
            ApplyComboPricing(group, group.Combo, quotes);
            changed |= oldOriginal != group.OriginalTotal || oldFinal != group.FinalTotal || oldDiscount != group.Discount ||
                oldItemValues.Any(old => group.Items.Any(item => item.Id == old.Id &&
                    (item.Price != old.Price || item.ComboDiscount != old.ComboDiscount)));
        }
        return changed;
    }

    private static void ApplyComboPricing(
        CartGroup group,
        Combo combo,
        IReadOnlyDictionary<PriceTargetKey, PriceQuote> quotes)
    {
        var grossLines = group.Items
            .OrderBy(item => item.Id)
            .Select(item =>
            {
                var quote = quotes[new PriceTargetKey(item.ProductId, item.ProductVariantId)];
                item.Price = quote.EffectivePrice;
                return (Item: item, Gross: quote.EffectivePrice * item.Quantity);
            })
            .ToList();
        var aggregateOriginal = grossLines.Sum(line => line.Gross);
        var unitOriginal = group.Quantity > 0 ? aggregateOriginal / group.Quantity : aggregateOriginal;
        var unitPrice = ComboPricingCalculator.Calculate(
            combo.PricingType,
            unitOriginal,
            combo.FixedPrice,
            combo.DiscountValue);

        group.ComboName = combo.Name;
        group.AllowCouponStacking = combo.AllowCouponStacking;
        group.UpdatedAt = DateTime.UtcNow;
        group.ExpiresAt = DateTime.UtcNow.AddDays(30);
        group.OriginalTotal = decimal.Round(unitPrice.OriginalTotal * group.Quantity, 2);
        group.FinalTotal = decimal.Round(unitPrice.FinalTotal * group.Quantity, 2);
        group.Discount = decimal.Round(group.OriginalTotal - group.FinalTotal, 2);

        if (aggregateOriginal <= 0)
        {
            foreach (var line in grossLines)
                line.Item.ComboDiscount = 0;
            return;
        }

        var allocated = 0m;
        for (var index = 0; index < grossLines.Count; index++)
        {
            var line = grossLines[index];
            var discount = index == grossLines.Count - 1
                ? group.Discount - allocated
                : decimal.Round(group.Discount * line.Gross / aggregateOriginal, 2);
            line.Item.ComboDiscount = Math.Max(0, discount);
            allocated += line.Item.ComboDiscount;
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
