using System.Security.Cryptography;
using System.Text;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Orders;
using Fruitables.Services.Pricing.Combos;
using Fruitables.Services.Pricing.Coupons;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using CartEntity = Fruitables.Models.Cart;

namespace Fruitables.Services.Orders.Cart;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICouponService _couponService;
    private readonly IProductPricingService _pricing;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly ApplicationDbContext? _dbContext;

    private bool UseTargetSchema => _dbContext?.Database.IsSqlServer() == true;

    public CartService(
        IUnitOfWork unitOfWork,
        ICouponService couponService,
        IProductPricingService pricing,
        IJsonDocumentSerializer? serializer = null,
        TimeProvider? timeProvider = null,
        ApplicationDbContext? dbContext = null)
    {
        _unitOfWork = unitOfWork;
        _couponService = couponService;
        _pricing = pricing;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dbContext = dbContext ?? (_unitOfWork as Repositories.UnitOfWork)?.Context;
    }

    public async Task<CartViewModel> GetCartAsync(string sessionId, string? district = null)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();
        if (await RefreshLinePricesAsync(lines))
        {
            document = WithLines(document, lines);
            await SaveDocumentAsync(cart, document);
        }

        return await BuildViewModelAsync(cart, document);
    }

    public async Task<CartMutationResult> AddToCartAsync(
        string sessionId,
        int productId,
        decimal quantity = 1m,
        int? variantId = null)
    {
        if (quantity <= 0)
            return CartMutationResult.Fail("Số lượng phải lớn hơn 0.");

        var cart = await GetOrCreateCartAsync(sessionId);
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && !p.IsDeleted);

        if (product == null)
            return CartMutationResult.Fail("Sản phẩm không tồn tại hoặc đã ngừng bán.");

        var activeVariants = product.Variants.Where(variant => variant.IsActive).ToList();
        var minimumStep = string.Equals(product.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase) ? 0.1m : 1m;
        if (!QuantityRules.IsValid(product.Unit, quantity, minimumStep))
            return CartMutationResult.Fail("Số lượng không hợp lệ.");

        ProductVariant? variant = null;
        if (activeVariants.Count > 0)
        {
            variant = activeVariants.FirstOrDefault(item => item.Id == variantId);
            if (variant == null)
                return CartMutationResult.Fail("Vui lòng chọn một biến thể đang bán.");
            if (variant.StockQuantity <= 0)
                return CartMutationResult.Fail("Biến thể đã hết hàng.");
        }
        else
        {
            if (variantId.HasValue)
                return CartMutationResult.Fail("Biến thể không hợp lệ.");
            if (product.StockQuantity <= 0)
                return CartMutationResult.Fail("Sản phẩm đã hết hàng.");
        }

        var quote = await _pricing.GetQuoteAsync(productId, variantId);
        if (quote == null)
            return CartMutationResult.Fail("Không thể xác định giá hiện tại của sản phẩm. Vui lòng tải lại trang.");

        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();
        var existing = lines.FirstOrDefault(item =>
            item.CartGroupId == null &&
            item.ProductId == productId &&
            item.ProductVariantId == variantId);

        var stock = variant?.StockQuantity ?? product.StockQuantity;
        var desiredQuantity = (existing?.Quantity ?? 0m) + quantity;
        if (!QuantityRules.IsValid(product.Unit, desiredQuantity, product.MinOrderQuantity))
            return CartMutationResult.Fail("Số lượng tối thiểu hoặc bước số lượng không hợp lệ.");

        if (existing != null)
        {
            ReplaceLine(lines, existing with
            {
                Quantity = Math.Min(desiredQuantity, stock),
                Price = quote.EffectivePrice
            });
        }
        else
        {
            lines.Add(new CartLineDocument
            {
                Id = document.NextLineId,
                ProductId = productId,
                ProductVariantId = variantId,
                Quantity = Math.Min(quantity, stock),
                Price = quote.EffectivePrice,
                ComboDiscount = 0
            });
            document = document.With(nextLineId: document.NextLineId + 1);
        }

        return await PersistAsync(cart, WithLines(document, lines), "Đã thêm sản phẩm vào giỏ hàng.");
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
            var minimumStep = string.Equals(product.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase) ? 0.1m : 1m;
            if (!QuantityRules.IsValid(product.Unit, request.Quantity, minimumStep))
                return CartMutationResult.Fail($"Số lượng của '{product.Name}' không hợp lệ.");

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

        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();
        var nextLineId = Math.Max(1, document.NextLineId);

        foreach (var request in groupedItems)
        {
            var product = products[request.ProductId];
            var variant = request.ProductVariantId.HasValue
                ? product.Variants.First(item => item.Id == request.ProductVariantId.Value)
                : null;
            var existing = lines.FirstOrDefault(item =>
                item.CartGroupId == null &&
                item.ProductId == request.ProductId &&
                item.ProductVariantId == request.ProductVariantId);
            var stock = variant?.StockQuantity ?? product.StockQuantity;
            var desiredQuantity = (existing?.Quantity ?? 0m) + request.Quantity;
            if (!QuantityRules.IsValid(product.Unit, desiredQuantity, product.MinOrderQuantity))
                return CartMutationResult.Fail($"Số lượng tối thiểu hoặc bước số lượng của '{product.Name}' không hợp lệ.");
            if (desiredQuantity > stock)
                return CartMutationResult.Fail($"'{product.Name}' không đủ tồn kho cho số lượng trong giỏ.");

            var key = new PriceTargetKey(request.ProductId, request.ProductVariantId);
            if (existing != null)
            {
                ReplaceLine(lines, existing with
                {
                    Quantity = desiredQuantity,
                    Price = quotes[key].EffectivePrice
                });
            }
            else
            {
                lines.Add(new CartLineDocument
                {
                    Id = nextLineId++,
                    ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId,
                    Quantity = request.Quantity,
                    Price = quotes[key].EffectivePrice,
                    ComboDiscount = 0
                });
            }
        }

        document = document.With(nextLineId: nextLineId);
        return await PersistAsync(cart, WithLines(document, lines), "Đã thêm toàn bộ combo vào giỏ hàng.");
    }

    public async Task<CartMutationResult> AddComboToCartAsync(string sessionId, int comboId)
    {
        var combo = await LoadComboAsync(comboId);
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

        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();

        foreach (var comboItem in combo.Items)
        {
            var alreadyInCart = lines
                .Where(item => item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId)
                .Sum(item => item.Quantity);
            var stock = comboItem.ProductVariant?.StockQuantity ?? comboItem.Product.StockQuantity;
            if (alreadyInCart + comboItem.Quantity > stock)
                return CartMutationResult.Fail($"'{comboItem.Product.Name}' không đủ tồn kho cho combo.");
        }

        var existingGroupId = lines
            .Where(item => item.ComboId == combo.Id && item.ComboRevision == combo.Revision && item.CartGroupId.HasValue)
            .Select(item => item.CartGroupId!.Value)
            .FirstOrDefault();

        if (existingGroupId == 0)
        {
            var groupId = Math.Max(1, document.NextGroupId);
            var nextLineId = Math.Max(1, document.NextLineId);
            var groupLines = new List<CartLineDocument>();
            foreach (var comboItem in combo.Items.OrderBy(item => item.SortOrder))
            {
                var key = new PriceTargetKey(comboItem.ProductId, comboItem.ProductVariantId);
                groupLines.Add(new CartLineDocument
                {
                    Id = nextLineId++,
                    ProductId = comboItem.ProductId,
                    ProductVariantId = comboItem.ProductVariantId,
                    CartGroupId = groupId,
                    ComboId = combo.Id,
                    ComboRevision = combo.Revision,
                    ComboName = combo.Name,
                    GroupQuantity = 1,
                    AllowCouponStacking = combo.AllowCouponStacking,
                    Quantity = comboItem.Quantity,
                    Price = quotes[key].EffectivePrice,
                    ComboDiscount = 0
                });
            }

            ApplyComboPricing(groupLines, combo, quotes, groupQuantity: 1);
            lines.AddRange(groupLines);
            document = document.With(nextLineId: nextLineId, nextGroupId: groupId + 1);
        }
        else
        {
            var groupLines = lines.Where(item => item.CartGroupId == existingGroupId).Select(CloneLine).ToList();
            var groupQuantity = (groupLines.FirstOrDefault()?.GroupQuantity ?? 1) + 1;
            foreach (var comboItem in combo.Items)
            {
                var line = groupLines.First(item =>
                    item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId);
                ReplaceLine(groupLines, line with { Quantity = comboItem.Quantity * groupQuantity });
            }

            ApplyComboPricing(groupLines, combo, quotes, groupQuantity);
            lines = lines.Where(item => item.CartGroupId != existingGroupId).Concat(groupLines).ToList();
        }

        return await PersistAsync(cart, WithLines(document, lines), $"Đã thêm combo '{combo.Name}' vào giỏ hàng.");
    }

    public async Task<CartMutationResult> UpdateComboQuantityAsync(string sessionId, int cartGroupId, int quantity)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();
        var groupLines = lines.Where(item => item.CartGroupId == cartGroupId).ToList();
        if (groupLines.Count == 0)
            return CartMutationResult.Fail("Không tìm thấy combo trong giỏ hàng.");

        if (quantity <= 0)
        {
            lines = lines.Where(item => item.CartGroupId != cartGroupId).ToList();
            return await PersistAsync(cart, WithLines(document, lines), "Đã xóa combo khỏi giỏ hàng.");
        }

        var comboId = groupLines[0].ComboId ?? 0;
        var comboRevision = groupLines[0].ComboRevision ?? 0;
        var combo = await LoadComboAsync(comboId);
        if (combo == null || !combo.IsAvailableAt(_timeProvider.GetUtcNow()) || combo.Revision != comboRevision)
            return CartMutationResult.Fail("Combo đã thay đổi hoặc ngừng bán. Vui lòng xóa và thêm lại.");

        var otherItems = lines.Where(item => item.CartGroupId != cartGroupId).ToList();
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

        var updatedGroup = groupLines.Select(CloneLine).ToList();
        foreach (var comboItem in combo.Items)
        {
            var line = updatedGroup.First(item =>
                item.ProductId == comboItem.ProductId && item.ProductVariantId == comboItem.ProductVariantId);
            ReplaceLine(updatedGroup, line with
            {
                Quantity = comboItem.Quantity * quantity,
                Price = quotes[new PriceTargetKey(comboItem.ProductId, comboItem.ProductVariantId)].EffectivePrice
            });
        }

        ApplyComboPricing(updatedGroup, combo, quotes, quantity);
        lines = lines.Where(item => item.CartGroupId != cartGroupId).Concat(updatedGroup).ToList();
        return await PersistAsync(cart, WithLines(document, lines), "Đã cập nhật số lượng combo.");
    }

    public async Task RemoveComboAsync(string sessionId, int cartGroupId)
    {
        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(c => c.SessionId == sessionId);
        if (cart == null) return;

        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Where(item => item.CartGroupId != cartGroupId).ToList();
        if (lines.Count == document.Lines.Count) return;
        await PersistAsync(cart, WithLines(document, lines), "Đã xóa combo.");
    }

    public async Task UpdateQuantityAsync(string sessionId, int cartItemId, decimal quantity)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Select(CloneLine).ToList();
        var item = lines.FirstOrDefault(line => line.Id == cartItemId && line.CartGroupId == null);
        if (item == null) return;

        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == item.ProductId);
        if (product == null)
        {
            lines.RemoveAll(line => line.Id == cartItemId);
            await PersistAsync(cart, WithLines(document, lines), "removed");
            return;
        }

        var variant = item.ProductVariantId.HasValue
            ? product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId.Value)
            : null;
        var isUnavailable = !product.IsActive || product.IsDeleted ||
            (item.ProductVariantId.HasValue && variant?.IsActive != true);
        var stock = variant?.StockQuantity ?? product.StockQuantity;

        if (quantity <= 0 || isUnavailable || stock <= 0)
            lines.RemoveAll(line => line.Id == cartItemId);
        else if (QuantityRules.IsValid(product.Unit, quantity, product.MinOrderQuantity))
            ReplaceLine(lines, item with { Quantity = Math.Min(quantity, stock) });

        await PersistAsync(cart, WithLines(document, lines), "updated");
    }

    public async Task RemoveFromCartAsync(string sessionId, int cartItemId)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var document = ReadLines(cart.LinesJson);
        var lines = document.Lines.Where(line => !(line.Id == cartItemId && line.CartGroupId == null)).ToList();
        if (lines.Count == document.Lines.Count) return;
        await PersistAsync(cart, WithLines(document, lines), "removed");
    }

    public async Task ClearCartAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(c => c.SessionId == sessionId);
        if (cart == null) return;
        await PersistAsync(cart, new CartLinesDocument(), "cleared");
    }

    public async Task<decimal> GetCartCountAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(c => c.SessionId == sessionId);
        if (cart == null) return 0;
        return ReadLines(cart.LinesJson).Lines.Sum(line => line.Quantity);
    }

    public async Task<CouponApplyResult> ApplyCouponAsync(string sessionId, string couponCode)
    {
        var cart = await GetOrCreateCartAsync(sessionId);
        var pricedCart = await GetCartAsync(sessionId);
        var eligibleItems = pricedCart.Items.Where(item => item.AllowCouponStacking).ToList();
        decimal subtotal = eligibleItems.Sum(item => item.Total);
        decimal itemCount = eligibleItems.Sum(item => item.Quantity);

        var result = await _couponService.ApplyCouponAsync(couponCode, subtotal, itemCount);
        if (result.Success)
        {
            cart.CouponCode = result.CouponCode;
            cart.CouponDiscount = result.DiscountAmount;
            cart.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return new CouponApplyResult
                {
                    Success = false,
                    Message = "Giỏ hàng đã được cập nhật bởi thao tác khác. Vui lòng thử lại."
                };
            }
        }

        return result;
    }

    public async Task RemoveCouponAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(c => c.SessionId == sessionId);
        if (cart == null) return;

        cart.CouponCode = null;
        cart.CouponDiscount = 0;
        cart.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // silent clear conflict; next read wins
        }
    }

    public Task<CartViewModel> RepriceForCheckoutAsync(string sessionId) => GetCartAsync(sessionId);

    private async Task<CartViewModel> BuildViewModelAsync(CartEntity cart, CartLinesDocument document)
    {
        var productIds = document.Lines.Select(line => line.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _unitOfWork.Products.Query()
                .Where(product => productIds.Contains(product.Id))
                .Include(product => product.Variants)
                .ToDictionaryAsync(product => product.Id);

        if (products.Count > 0)
            ProductAggregateJson.Hydrate(products.Values, _serializer);

        var comboIds = document.Lines.Where(line => line.ComboId.HasValue).Select(line => line.ComboId!.Value).Distinct().ToList();
        var combos = await LoadCombosAsync(comboIds);

        var cartViewModel = new CartViewModel
        {
            Items = document.Lines.Select(line =>
            {
                products.TryGetValue(line.ProductId, out var product);
                var variant = line.ProductVariantId.HasValue
                    ? product?.Variants.FirstOrDefault(item => item.Id == line.ProductVariantId.Value)
                    : null;
                combos.TryGetValue(line.ComboId ?? 0, out var combo);
                var comboValid = line.CartGroupId == null ||
                    (combo != null &&
                     combo.IsAvailableAt(_timeProvider.GetUtcNow()) &&
                     line.ComboRevision == combo.Revision);

                return new CartItemViewModel
                {
                    CartItemId = line.Id,
                    ProductId = line.ProductId,
                    ProductVariantId = line.ProductVariantId,
                    CartGroupId = line.CartGroupId,
                    SourceComboId = line.ComboId,
                    ComboName = line.ComboName,
                    ComboRevision = line.ComboRevision,
                    ComboQuantity = line.GroupQuantity,
                    AllowCouponStacking = line.AllowCouponStacking ?? true,
                    ComboDiscount = line.ComboDiscount,
                    VariantName = variant?.Name,
                    VariantSKU = variant?.SKU,
                    ProductName = product?.Name ?? string.Empty,
                    Unit = product?.Unit ?? "kg",
                    MinOrderQuantity = product?.MinOrderQuantity ?? 1,
                    ProductSlug = product?.Slug ?? string.Empty,
                    ProductImage = product?.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                        ?? product?.Images.FirstOrDefault()?.ImageUrl
                        ?? string.Empty,
                    Price = line.Price,
                    Quantity = line.Quantity,
                    StockQuantity = variant?.StockQuantity ?? product?.StockQuantity ?? 0,
                    IsAvailable = product is { IsActive: true, IsDeleted: false } &&
                        (line.ProductVariantId.HasValue
                            ? variant?.IsActive == true
                            : product.Variants.All(v => !v.IsActive)) &&
                        comboValid
                };
            }).ToList()
        };

        cartViewModel.Groups = document.Lines
            .Where(line => line.CartGroupId.HasValue)
            .GroupBy(line => line.CartGroupId!.Value)
            .Select(group =>
            {
                var first = group.First();
                combos.TryGetValue(first.ComboId ?? 0, out var combo);
                var items = cartViewModel.Items.Where(item => item.CartGroupId == group.Key).ToList();
                return new CartGroupViewModel
                {
                    Id = group.Key,
                    ComboId = first.ComboId ?? 0,
                    ComboRevision = first.ComboRevision ?? 0,
                    ComboName = first.ComboName ?? string.Empty,
                    Quantity = first.GroupQuantity ?? 1,
                    OriginalTotal = first.GroupOriginalTotal ?? items.Sum(item => item.Price * item.Quantity),
                    FinalTotal = first.GroupFinalTotal ?? items.Sum(item => item.Total),
                    Discount = first.GroupDiscount ?? items.Sum(item => item.ComboDiscount),
                    AllowCouponStacking = first.AllowCouponStacking ?? true,
                    IsValid = combo != null &&
                        combo.IsAvailableAt(_timeProvider.GetUtcNow()) &&
                        first.ComboRevision == combo.Revision &&
                        items.All(item => item.IsAvailable || item.SourceComboId == first.ComboId),
                    Items = items
                };
            })
            .ToList();

        cartViewModel.Subtotal = cartViewModel.Items.Sum(i => i.Total);

        var couponEligibleItems = cartViewModel.Items.Where(item => item.AllowCouponStacking).ToList();
        var couponEligibleSubtotal = couponEligibleItems.Sum(item => item.Total);
        var couponEligibleCount = couponEligibleItems.Sum(item => item.Quantity);
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            var coupon = await _couponService.ApplyCouponAsync(cart.CouponCode, couponEligibleSubtotal, couponEligibleCount);
            if (coupon.Success)
                cart.CouponDiscount = coupon.DiscountAmount;
            else
            {
                cart.CouponCode = null;
                cart.CouponDiscount = 0;
            }

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // next load will recompute coupon state
            }
        }

        cartViewModel.ShippingInfo = null;
        cartViewModel.ShippingFee = 0m;
        cartViewModel.CouponCode = cart.CouponCode;
        cartViewModel.Discount = cart.CouponDiscount;
        cartViewModel.Total = cartViewModel.Subtotal + cartViewModel.ShippingFee - cartViewModel.Discount;

        var tokenSource = string.Join('|', cartViewModel.Items.OrderBy(i => i.CartItemId)
            .Select(i => $"{i.CartItemId}:{i.ProductId}:{i.ProductVariantId}:{i.CartGroupId}:{i.ComboRevision}:{i.Quantity}:{i.Price}:{i.ComboDiscount}:{i.StockQuantity}:{i.IsAvailable}"))
            + $"|{cartViewModel.CouponCode}:{cartViewModel.Discount}";
        cartViewModel.PricingToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenSource)));
        return cartViewModel;
    }

    private async Task<bool> RefreshLinePricesAsync(List<CartLineDocument> lines)
    {
        if (lines.Count == 0) return false;

        var targets = lines.Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId)).Distinct().ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        var changed = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var item = lines[index];
            if (item.CartGroupId != null) continue;
            if (!quotes.TryGetValue(new PriceTargetKey(item.ProductId, item.ProductVariantId), out var quote))
                continue;
            if (item.Price != quote.EffectivePrice || item.ComboDiscount != 0)
            {
                lines[index] = item with { Price = quote.EffectivePrice, ComboDiscount = 0 };
                changed = true;
            }
        }

        var comboIds = lines.Where(line => line.ComboId.HasValue).Select(line => line.ComboId!.Value).Distinct().ToList();
        if (comboIds.Count == 0) return changed;

        var combos = await LoadCombosAsync(comboIds);

        foreach (var group in lines.Where(line => line.CartGroupId.HasValue).GroupBy(line => line.CartGroupId!.Value))
        {
            var groupLines = group.Select(CloneLine).ToList();
            var first = groupLines[0];
            if (!combos.TryGetValue(first.ComboId ?? 0, out var combo))
                continue;
            if (!combo.IsAvailableAt(_timeProvider.GetUtcNow()) || first.ComboRevision != combo.Revision)
                continue;
            if (groupLines.Any(item => !quotes.ContainsKey(new PriceTargetKey(item.ProductId, item.ProductVariantId))))
                continue;

            var old = groupLines.Select(item => (item.Id, item.Price, item.ComboDiscount, item.GroupOriginalTotal, item.GroupFinalTotal, item.GroupDiscount)).ToList();
            ApplyComboPricing(groupLines, combo, quotes, first.GroupQuantity ?? 1);
            for (var i = 0; i < groupLines.Count; i++)
            {
                var updated = groupLines[i];
                var existingIndex = lines.FindIndex(line => line.Id == updated.Id);
                if (existingIndex >= 0)
                    lines[existingIndex] = updated;
            }

            changed |= old.Any(before =>
            {
                var after = groupLines.First(item => item.Id == before.Id);
                return before.Price != after.Price ||
                       before.ComboDiscount != after.ComboDiscount ||
                       before.GroupOriginalTotal != after.GroupOriginalTotal ||
                       before.GroupFinalTotal != after.GroupFinalTotal ||
                       before.GroupDiscount != after.GroupDiscount;
            });
        }

        return changed;
    }

    private static void ApplyComboPricing(
        List<CartLineDocument> groupLines,
        Combo combo,
        IReadOnlyDictionary<PriceTargetKey, PriceQuote> quotes,
        int groupQuantity)
    {
        var grossLines = groupLines
            .OrderBy(item => item.Id)
            .Select(item =>
            {
                var quote = quotes[new PriceTargetKey(item.ProductId, item.ProductVariantId)];
                var updated = item with { Price = quote.EffectivePrice };
                return (Item: updated, Gross: quote.EffectivePrice * updated.Quantity);
            })
            .ToList();

        var aggregateOriginal = grossLines.Sum(line => line.Gross);
        var unitOriginal = groupQuantity > 0 ? aggregateOriginal / groupQuantity : aggregateOriginal;
        var unitPrice = ComboPricingCalculator.Calculate(
            combo.PricingType,
            unitOriginal,
            combo.FixedPrice,
            combo.DiscountValue);

        var originalTotal = decimal.Round(unitPrice.OriginalTotal * groupQuantity, 2);
        var finalTotal = decimal.Round(unitPrice.FinalTotal * groupQuantity, 2);
        var discount = decimal.Round(originalTotal - finalTotal, 2);

        var allocated = 0m;
        var result = new List<CartLineDocument>(grossLines.Count);
        for (var index = 0; index < grossLines.Count; index++)
        {
            var line = grossLines[index];
            var lineDiscount = aggregateOriginal <= 0
                ? 0m
                : index == grossLines.Count - 1
                    ? discount - allocated
                    : decimal.Round(discount * line.Gross / aggregateOriginal, 2);
            allocated += Math.Max(0, lineDiscount);
            result.Add(line.Item with
            {
                ComboId = combo.Id,
                ComboRevision = combo.Revision,
                ComboName = combo.Name,
                GroupQuantity = groupQuantity,
                GroupOriginalTotal = originalTotal,
                GroupFinalTotal = finalTotal,
                GroupDiscount = discount,
                AllowCouponStacking = combo.AllowCouponStacking,
                ComboDiscount = Math.Max(0, lineDiscount)
            });
        }

        groupLines.Clear();
        groupLines.AddRange(result);
    }

    private async Task<Combo?> LoadComboAsync(int comboId)
    {
        if (!UseTargetSchema)
        {
            return await _unitOfWork.Combos.Query()
                .Where(item => item.Id == comboId)
                .Include(item => item.Items)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.Variants)
                .Include(item => item.Items)
                    .ThenInclude(item => item.ProductVariant)
                .FirstOrDefaultAsync();
        }

        var promotion = await _dbContext!.Promotions.AsNoTracking()
            .Where(item => item.Type == "combo" &&
                (item.Id == comboId || item.Code == $"combo:{comboId}"))
            .FirstOrDefaultAsync();
        return promotion is null ? null : await ToComboAsync(promotion);
    }

    private async Task<Dictionary<int, Combo>> LoadCombosAsync(IReadOnlyCollection<int> comboIds)
    {
        if (comboIds.Count == 0)
            return new Dictionary<int, Combo>();
        if (!UseTargetSchema)
        {
            return await _unitOfWork.Combos.Query()
                .Where(combo => comboIds.Contains(combo.Id))
                .ToDictionaryAsync(combo => combo.Id);
        }

        var idSet = comboIds.ToHashSet();
        var promotions = await _dbContext!.Promotions.AsNoTracking()
            .Where(item => item.Type == "combo")
            .ToListAsync();
        var result = new Dictionary<int, Combo>();
        foreach (var promotion in promotions)
        {
            var legacyId = TryLegacyComboId(promotion.Code);
            if (!idSet.Contains(promotion.Id) && (!legacyId.HasValue || !idSet.Contains(legacyId.Value)))
                continue;
            var combo = await ToComboAsync(promotion);
            result[promotion.Id] = combo;
            if (legacyId.HasValue)
                result[legacyId.Value] = combo;
        }
        return result;
    }

    private async Task<Combo> ToComboAsync(Promotion promotion)
    {
        var payload = _serializer.Deserialize<ComboPayload>(promotion.PayloadJson);
        var productIds = payload.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await _unitOfWork.Products.Query()
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Include(product => product.Variants)
            .ToDictionaryAsync(product => product.Id);
        ProductAggregateJson.Hydrate(products.Values, _serializer);

        var combo = new Combo
        {
            Id = promotion.Id,
            Name = payload.Name,
            Slug = payload.Slug,
            Description = payload.Description,
            ImageUrl = payload.ImageUrl,
            IsActive = promotion.IsActive && payload.IsActive,
            Status = payload.Status,
            StartsAt = payload.StartsAt,
            EndsAt = payload.EndsAt,
            PricingType = payload.PricingType,
            FixedPrice = payload.FixedPrice,
            DiscountValue = payload.DiscountValue,
            AllowCouponStacking = payload.AllowCouponStacking,
            Revision = payload.Revision,
            SortOrder = payload.SortOrder,
            CreatedAt = promotion.CreatedAt,
            UpdatedAt = promotion.UpdatedAt
        };
        combo.Items = payload.Items.OrderBy(item => item.SortOrder).Select((item, index) =>
        {
            products.TryGetValue(item.ProductId, out var product);
            return new ComboItem
            {
                Id = index + 1,
                ComboId = combo.Id,
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                SortOrder = item.SortOrder,
                Product = product!,
                ProductVariant = product?.Variants.FirstOrDefault(variant => variant.Id == item.ProductVariantId)
            };
        }).ToList();
        return combo;
    }

    private static int? TryLegacyComboId(string? code) =>
        code is not null && code.StartsWith("combo:", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(code["combo:".Length..], out var id) && id > 0 ? id : null;

    private async Task<CartMutationResult> PersistAsync(CartEntity cart, CartLinesDocument document, string successMessage)
    {
        try
        {
            await SaveDocumentAsync(cart, document);
            return CartMutationResult.Ok(successMessage);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CartMutationResult.Fail("Giỏ hàng đã được cập nhật bởi thao tác khác. Vui lòng tải lại.");
        }
    }

    private async Task SaveDocumentAsync(CartEntity cart, CartLinesDocument document)
    {
        cart.LinesJson = _serializer.Serialize(document);
        cart.UpdatedAt = DateTime.UtcNow;
        cart.RowVersion = Guid.NewGuid().ToByteArray();
        await _unitOfWork.SaveChangesAsync();
    }

    private CartLinesDocument ReadLines(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new CartLinesDocument();

        var document = _serializer.Deserialize<CartLinesDocument>(json);
        var lines = document.Lines ?? [];
        var nextLineId = document.NextLineId > 0
            ? document.NextLineId
            : Math.Max(1, lines.Select(line => line.Id).DefaultIfEmpty(0).Max() + 1);
        var nextGroupId = document.NextGroupId > 0
            ? document.NextGroupId
            : Math.Max(1, lines.Where(line => line.CartGroupId.HasValue).Select(line => line.CartGroupId!.Value).DefaultIfEmpty(0).Max() + 1);

        // Backfill-era documents may omit line ids; assign stable ones in memory.
        if (lines.Any(line => line.Id <= 0))
        {
            var assigned = nextLineId;
            lines = lines.Select(line => line.Id > 0 ? line : line with { Id = assigned++ }).ToList();
            nextLineId = assigned;
        }

        return document.With(lines: lines, nextLineId: nextLineId, nextGroupId: nextGroupId);
    }

    private static CartLinesDocument WithLines(CartLinesDocument document, List<CartLineDocument> lines) =>
        document.With(lines: lines);

    private static CartLineDocument CloneLine(CartLineDocument line) => line with { };

    private static void ReplaceLine(List<CartLineDocument> lines, CartLineDocument updated)
    {
        var index = lines.FindIndex(line => line.Id == updated.Id &&
            line.ProductId == updated.ProductId &&
            line.ProductVariantId == updated.ProductVariantId &&
            line.CartGroupId == updated.CartGroupId);
        if (index < 0)
            index = lines.FindIndex(line =>
                line.ProductId == updated.ProductId &&
                line.ProductVariantId == updated.ProductVariantId &&
                line.CartGroupId == updated.CartGroupId);
        if (index >= 0)
            lines[index] = updated;
        else
            lines.Add(updated);
    }

    private async Task<CartEntity> GetOrCreateCartAsync(string sessionId)
    {
        var cart = await _unitOfWork.Carts.Query().FirstOrDefaultAsync(c => c.SessionId == sessionId);
        if (cart != null)
            return cart;

        cart = new CartEntity
        {
            SessionId = sessionId,
            LinesJson = _serializer.Serialize(new CartLinesDocument()),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        await _unitOfWork.Carts.AddAsync(cart);
        await _unitOfWork.SaveChangesAsync();
        return cart;
    }
}
