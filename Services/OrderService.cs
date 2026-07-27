using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Fruitables.Helpers;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services;

public class OrderService : IOrderService
{
    private const string PaymentCodePrefix = "FTB";
    private const string PaymentCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICartService _cartService;
    private readonly IRealtimeNotifier _notifier;
    private readonly IProductPricingService _pricing;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IUnitOfWork unitOfWork,
        ICartService cartService,
        IRealtimeNotifier notifier,
        IProductPricingService pricing,
        ILogger<OrderService> logger)
    {
        _unitOfWork = unitOfWork;
        _cartService = cartService;
        _notifier = notifier;
        _pricing = pricing;
        _logger = logger;
    }

    private sealed record StockNotification(
        int ProductId,
        int StockQuantity,
        int? ProductVariantId);

    public async Task<Order> CreateOrderAsync(CheckoutViewModel model, string sessionId, int? userId = null)
    {
        Order? committedOrder = null;
        var stockNotifications = new List<StockNotification>();
        var providerName = _unitOfWork.DatabaseProviderName ?? string.Empty;
        var isInMemory = providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

        if (!isInMemory)
        {
            transaction = await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        }

        try
        {
        var cart = await _cartService.RepriceForCheckoutAsync(sessionId)
            ?? await _cartService.GetCartAsync(sessionId);

        if (!string.IsNullOrWhiteSpace(cart.PricingToken) &&
            !string.Equals(model.PricingToken, cart.PricingToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Giá hoặc mã giảm giá vừa thay đổi. Vui lòng kiểm tra tổng tiền và xác nhận lại.");
        }

        if (cart.Items.Any(item => !item.IsAvailable))
            throw new InvalidOperationException("Một số sản phẩm hoặc combo không còn hợp lệ. Vui lòng cập nhật giỏ hàng.");

        var targets = cart.Items
            .Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .Distinct()
            .ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);

        foreach (var item in cart.Items)
        {
            var key = new PriceTargetKey(item.ProductId, item.ProductVariantId);
            if (!quotes.TryGetValue(key, out var quote))
                throw new InvalidOperationException("Một số sản phẩm không còn được bán.");

            if (item.Price != quote.EffectivePrice)
                throw new InvalidOperationException(
                    "Giá vừa thay đổi. Vui lòng quay lại bước thanh toán và xác nhận lại.");
        }

        // Load products batch and validate before any mutation.
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.Query()
            .Where(p => productIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id);

        var missingProductIds = productIds.Except(products.Keys).ToList();
        var variantIds = cart.Items.Where(i => i.ProductVariantId.HasValue)
            .Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variants = await _unitOfWork.ProductVariants.Query()
            .Where(v => variantIds.Contains(v.Id) && v.IsActive)
            .ToDictionaryAsync(v => v.Id);
        var variantManagedProductIds = await _unitOfWork.ProductVariants.Query()
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .Select(v => v.ProductId).Distinct().ToListAsync();
        if (variantIds.Except(variants.Keys).Any())
            throw new InvalidOperationException("Một số biến thể không còn bán.");
        if (cart.Items.Any(i => !i.ProductVariantId.HasValue && variantManagedProductIds.Contains(i.ProductId)))
            throw new InvalidOperationException("Một số sản phẩm nay yêu cầu chọn biến thể. Vui lòng cập nhật lại giỏ hàng.");
        if (missingProductIds.Any())
        {
            throw new InvalidOperationException("Một số sản phẩm không tồn tại trong hệ thống.");
        }

        // Group cart lines by product so duplicate lines don't bypass the stock check.
        // Reused for validation, in-memory mutation, and atomic conditional update.
        var productGroups = cart.Items
            .GroupBy(i => new { i.ProductId, i.ProductVariantId })
            .Select(g => new
            {
                ProductId = g.Key.ProductId,
                ProductVariantId = g.Key.ProductVariantId,
                Quantity = g.Sum(i => i.Quantity),
                ProductNames = g.Select(i => i.ProductName).Distinct().ToList()
            })
            .ToList();

        var insufficientGroups = productGroups
            .Where(g => g.ProductVariantId.HasValue
                ? !variants.ContainsKey(g.ProductVariantId.Value) || variants[g.ProductVariantId.Value].StockQuantity < g.Quantity
                : !products.ContainsKey(g.ProductId) || products[g.ProductId].StockQuantity < g.Quantity)
            .ToList();
        if (insufficientGroups.Any())
        {
            var itemNames = string.Join(", ", insufficientGroups.SelectMany(g => g.ProductNames).Distinct());
            throw new InvalidOperationException($"Các sản phẩm sau không đủ số lượng tồn kho: {itemNames}");
        }

        // Use shipping fee from model.Cart if available (snapshot from checkout), otherwise from fresh cart.
        var shippingFee = model.Cart?.ShippingFee ?? cart.ShippingFee;

        var order = new Order
        {
            UserId = userId,
            OrderNumber = GenerateOrderNumber(),
            Status = OrderStatus.Pending,
            Subtotal = cart.Subtotal,
            ShippingFee = shippingFee, // Snapshot shipping fee (Requirements 6.3, 8.1, 8.2).
            Discount = cart.Discount,
            Total = cart.Subtotal + shippingFee - cart.Discount,
            PaymentMethod = model.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            ShippingMethod = model.ShippingMethod,
            PaymentCode = model.PaymentMethod == PaymentMethod.BankTransfer
                ? await GenerateUniquePaymentCodeAsync()
                : null,
            Notes = model.Notes
        };

        // Build order items from authoritative quotes (stock deduction below).
        foreach (var item in cart.Items)
        {
            var key = new PriceTargetKey(item.ProductId, item.ProductVariantId);
            var quote = quotes[key];

            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                VariantName = item.VariantName,
                VariantSKU = item.VariantSKU,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                BasePrice = quote.BasePrice,
                Price = quote.EffectivePrice,
                PromotionDiscount = (quote.BasePrice - quote.EffectivePrice) * item.Quantity,
                PriceScheduleId = quote.ScheduleId,
                SourceComboId = item.SourceComboId,
                ComboNameSnapshot = item.ComboName,
                ComboRevision = item.ComboRevision,
                ComboQuantity = item.ComboQuantity,
                ComboDiscount = item.ComboDiscount,
                Total = quote.EffectivePrice * item.Quantity - item.ComboDiscount
            });
        }

        var authoritativeSubtotal = order.Items.Sum(item => item.Total);
        order.Subtotal = authoritativeSubtotal;
        order.Total = authoritativeSubtotal + shippingFee - cart.Discount;

        // Resolve the shipping address. New address is queued but not saved until the transaction commits.
        Address? shippingAddress = null;

        if (model.SelectedAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.Query()
                .FirstOrDefaultAsync(a =>
                    a.Id == model.SelectedAddressId.Value
                    && userId.HasValue
                    && a.UserId == userId.Value);
            if (shippingAddress == null)
                throw new InvalidOperationException("Dia chi giao hang khong hop le.");

            order.AddressId = shippingAddress.Id;
        }
        else if (!string.IsNullOrEmpty(model.StreetAddress))
        {
            shippingAddress = new Address
            {
                UserId = userId,
                FullName = !string.IsNullOrEmpty(model.FullName) ? model.FullName : model.FirstName.Trim(),
                Phone = model.Mobile,
                ProvinceCode = model.ProvinceCode,
                ProvinceName = model.ProvinceName ?? string.Empty,
                CommuneCode = model.CommuneCode,
                CommuneName = model.CommuneName ?? string.Empty,
                StreetAddress = model.StreetAddress,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            };
        }

        if (shippingAddress != null)
        {
            order.ShippingSnapshot = AddressSnapshotHelper.ToSnapshot(shippingAddress);
        }

        // Stage the new address + order + stock changes; one save commits all of them.
        if (model.SelectedAddressId == null && !string.IsNullOrEmpty(model.StreetAddress) && shippingAddress != null)
        {
            await _unitOfWork.Addresses.AddAsync(shippingAddress);
            order.Address = shippingAddress;
        }

        await _unitOfWork.Orders.AddAsync(order);

            if (isInMemory)
            {
                // InMemory: mutate tracked entities directly.
                foreach (var group in productGroups)
                {
                    if (group.ProductVariantId.HasValue && variants.TryGetValue(group.ProductVariantId.Value, out var variant))
                        variant.StockQuantity -= group.Quantity;
                    else if (products.TryGetValue(group.ProductId, out var product))
                        product.StockQuantity -= group.Quantity;
                }
            }
            else
            {
                // Atomic conditional update: deduct stock only if sufficient remains.
                // Prevents oversell when two requests race on the same product.
                foreach (var group in productGroups)
                {
                    var rows = group.ProductVariantId.HasValue
                        ? await _unitOfWork.ProductVariants.Query()
                            .Where(v => v.Id == group.ProductVariantId.Value && v.ProductId == group.ProductId &&
                                v.IsActive && v.Product.IsActive && !v.Product.IsDeleted && v.StockQuantity >= group.Quantity)
                            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - group.Quantity))
                        : await _unitOfWork.Products.Query()
                            .Where(p => p.Id == group.ProductId && p.IsActive && !p.IsDeleted &&
                                !p.Variants.Any(v => v.IsActive) && p.StockQuantity >= group.Quantity)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - group.Quantity));

                    if (rows == 0)
                    {
                        throw new InvalidOperationException($"Sản phẩm mã {group.ProductId} không đủ số lượng tồn kho.");
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();

            foreach (var group in productGroups)
            {
                if (group.ProductVariantId.HasValue)
                {
                    var variant = variants[group.ProductVariantId.Value];
                    var finalStock = isInMemory
                        ? variant.StockQuantity
                        : variant.StockQuantity - group.Quantity;

                    stockNotifications.Add(new StockNotification(
                        group.ProductId,
                        finalStock,
                        group.ProductVariantId));
                }
                else
                {
                    var product = products[group.ProductId];
                    var finalStock = isInMemory
                        ? product.StockQuantity
                        : product.StockQuantity - group.Quantity;

                    stockNotifications.Add(new StockNotification(
                        group.ProductId,
                        finalStock,
                        null));
                }
            }

            if (transaction != null)
                await transaction.CommitAsync();

            committedOrder = order;
        }
        catch
        {
            if (transaction != null)
                await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }

        if (committedOrder == null)
            throw new InvalidOperationException("KhÃ´ng thá»ƒ xÃ¡c nháº­n Ä‘Æ¡n hÃ ng Ä‘Ã£ Ä‘Æ°á»£c lÆ°u.");

        await RunPostCommitActionsAsync(
            committedOrder,
            sessionId,
            stockNotifications);

        return committedOrder;
    }

    private async Task RunPostCommitActionsAsync(
        Order order,
        string sessionId,
        IReadOnlyList<StockNotification> stockNotifications)
    {
        try
        {
            await _cartService.ClearCartAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Order {OrderId} committed but cart cleanup failed for session {SessionId}",
                order.Id,
                sessionId);
        }

        try
        {
            await _notifier.NotifyOrderCreatedAsync(order.Id, order.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Order {OrderId} committed but order-created notification failed",
                order.Id);
        }

        foreach (var stock in stockNotifications)
        {
            try
            {
                await _notifier.NotifyStockChangedAsync(
                    stock.ProductId,
                    stock.StockQuantity,
                    stock.ProductVariantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Order {OrderId} committed but stock notification failed for product {ProductId}, variant {VariantId}",
                    order.Id,
                    stock.ProductId,
                    stock.ProductVariantId);
            }
        }
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetOrderByNumberAsync(string orderNumber)
    {
        return await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
    }

    public async Task<List<Order>> GetOrdersByUserIdAsync(int userId)
    {
        return await _unitOfWork.Orders.Query()
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Address?> GetShippingAddressFromSnapshotAsync(int orderId)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null || string.IsNullOrEmpty(order.ShippingSnapshot))
            return null;

        return AddressSnapshotHelper.FromSnapshot(order.ShippingSnapshot);
    }

    private async Task<string> GenerateUniquePaymentCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = PaymentCodePrefix + RandomNumberGenerator.GetString(PaymentCodeAlphabet, 8);
            var exists = await _unitOfWork.Orders.Query().AnyAsync(o => o.PaymentCode == code);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Không thể tạo mã thanh toán. Vui lòng thử lại.");
    }

    private static string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.UtcNow.AddHours(7):yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }

    private static decimal GetShippingFee(ShippingMethod method)
    {
        return method switch
        {
            ShippingMethod.Free => 0,
            ShippingMethod.FlatRate => 15.00m,
            ShippingMethod.LocalPickup => 8.00m,
            _ => 15.00m
        };
    }
}
