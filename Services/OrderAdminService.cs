using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;

namespace Fruitables.Services;

public class OrderAdminService : IOrderAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrderLogService _logService;
    private readonly IRealtimeNotifier _notifier;

    public OrderAdminService(ApplicationDbContext context, IOrderLogService logService, IRealtimeNotifier notifier)
    {
        _context = context;
        _logService = logService;
        _notifier = notifier;
    }

    public async Task<OrderListResult> GetOrdersAsync(OrderListRequest request)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(o => o.OrderNumber.Contains(request.Search));
        }

        // Filter by Status
        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        // Filter by PaymentStatus
        if (request.PaymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == request.PaymentStatus.Value);
        }

        // Filter by Date Range
        if (request.FromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= request.ToDate.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "total" => request.SortDescending
                ? query.OrderByDescending(o => o.Total)
                : query.OrderBy(o => o.Total),
            "status" => request.SortDescending
                ? query.OrderByDescending(o => o.Status)
                : query.OrderBy(o => o.Status),
            _ => request.SortDescending
                ? query.OrderByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.CreatedAt)
        };

        // Pagination
        var orders = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new OrderListResult
        {
            Orders = orders,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.User)
            .Include(o => o.Address)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order?> GetOrderWithHistoryAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Images)
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.StatusHistory)
                .ThenInclude(h => h.Admin)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<OrderResult> UpdateOrderStatusAsync(UpdateOrderStatusRequest request)
    {
        // The InMemory provider (used by unit tests) does not support transactions; fall back to a single save.
        var supportsTransactions = !(_context.Database.ProviderName?.Contains("InMemory") ?? false);
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (supportsTransactions)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            // Get order with items
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId);

            if (order == null)
                return OrderResult.Fail(OrderErrorType.NotFound, "Đơn hàng không tồn tại");

            // Validate status transition
            if (!IsValidStatusTransition(order.Status, request.NewStatus))
                return OrderResult.Fail(OrderErrorType.InvalidStatusTransition,
                    $"Không thể chuyển từ {order.Status} sang {request.NewStatus}");

            // Stock is deducted when the order is placed in OrderService.CreateOrderAsync.
            // Cancel → restore; restore from cancelled → deduct.
            Dictionary<(int ProductId, int? VariantId), int>? changedStocks = null;
            if (request.NewStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
            {
                changedStocks = await RestoreStockForOrder(order);
            }
            else if (order.Status == OrderStatus.Cancelled && request.NewStatus != OrderStatus.Cancelled)
            {
                var deduction = await CheckAndDeductStockForOrder(order);
                if (deduction.InsufficientItems.Any())
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    return OrderResult.FailWithInsufficientStock(deduction.InsufficientItems);
                }
                changedStocks = deduction.StockLevels;
            }

            // Stage status update + history so a single SaveChanges commits the full atomic flow.
            var oldStatus = order.Status;
            order.Status = request.NewStatus;
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                OldStatus = oldStatus,
                NewStatus = request.NewStatus,
                AdminId = request.AdminId,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            // Realtime notification
            await _notifier.NotifyOrderUpdatedAsync(order.Id, order.UserId, order.Status.ToString());
            
            // If status cancelled or restored, stock changed. Broadcast.
            if (request.NewStatus == OrderStatus.Cancelled || oldStatus == OrderStatus.Cancelled)
            {
                await NotifyStockForItemsAsync(order.Items, changedStocks);
            }

            return OrderResult.Ok(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            return OrderResult.Fail(OrderErrorType.ConcurrencyConflict,
                "Đơn hàng đã được cập nhật bởi người dùng khác. Vui lòng tải lại dữ liệu.");
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private async Task NotifyStockForItemsAsync(
        IEnumerable<OrderItem> items,
        IReadOnlyDictionary<(int ProductId, int? VariantId), int>? knownStockLevels = null)
    {
        var targets = items.GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .Select(group => group.Key).ToList();
        if (knownStockLevels != null)
        {
            foreach (var target in targets)
            {
                if (!knownStockLevels.TryGetValue((target.ProductId, target.ProductVariantId), out var stock))
                    continue;
                if (target.ProductVariantId.HasValue)
                    await _notifier.NotifyStockChangedAsync(target.ProductId, stock, target.ProductVariantId);
                else
                    await _notifier.NotifyStockChangedAsync(target.ProductId, stock);
            }
            return;
        }

        var isInMemory = (_context.Database.ProviderName ?? string.Empty).Contains("InMemory");
        var productIds = targets.Select(target => target.ProductId).Distinct().ToList();
        var variantIds = targets.Where(target => target.ProductVariantId.HasValue).Select(target => target.ProductVariantId!.Value).Distinct().ToList();
        var products = isInMemory
            ? _context.Products.Local.ToDictionary(product => product.Id)
            : await _context.Products.AsNoTracking().Where(product => productIds.Contains(product.Id)).ToDictionaryAsync(product => product.Id);
        var variants = isInMemory
            ? _context.ProductVariants.Local.ToDictionary(variant => variant.Id)
            : await _context.ProductVariants.AsNoTracking().Where(variant => variantIds.Contains(variant.Id)).ToDictionaryAsync(variant => variant.Id);
        foreach (var target in targets)
        {
            if (target.ProductVariantId.HasValue && variants.TryGetValue(target.ProductVariantId.Value, out var variant))
                await _notifier.NotifyStockChangedAsync(target.ProductId, variant.StockQuantity, target.ProductVariantId);
            else if (products.TryGetValue(target.ProductId, out var product))
                await _notifier.NotifyStockChangedAsync(target.ProductId, product.StockQuantity);
        }
    }

    private async Task<Dictionary<(int ProductId, int? VariantId), int>> RestoreStockForOrder(Order order)
    {
        var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        var variantIds = order.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variants = await _context.ProductVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);

        var isInMemory = (_context.Database.ProviderName ?? string.Empty).Contains("InMemory");
        var groups = order.Items.GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .Select(group => new { group.Key.ProductId, group.Key.ProductVariantId, Quantity = group.Sum(item => item.Quantity) });
        var stockLevels = new Dictionary<(int ProductId, int? VariantId), int>();
        foreach (var group in groups)
        {
            if (group.ProductVariantId.HasValue && variants.TryGetValue(group.ProductVariantId.Value, out var variant))
            {
                if (isInMemory) variant.StockQuantity += group.Quantity;
                else
                {
                    await _context.ProductVariants.Where(v => v.Id == variant.Id)
                        .ExecuteUpdateAsync(update => update.SetProperty(v => v.StockQuantity, v => v.StockQuantity + group.Quantity));
                    SynchronizeStock(variant, variant.StockQuantity + group.Quantity);
                }
                stockLevels[(group.ProductId, group.ProductVariantId)] = variant.StockQuantity;
            }
            else if (products.TryGetValue(group.ProductId, out var product))
            {
                if (isInMemory) product.StockQuantity += group.Quantity;
                else
                {
                    await _context.Products.Where(p => p.Id == product.Id)
                        .ExecuteUpdateAsync(update => update.SetProperty(p => p.StockQuantity, p => p.StockQuantity + group.Quantity));
                    SynchronizeStock(product, product.StockQuantity + group.Quantity);
                }
                stockLevels[(group.ProductId, null)] = product.StockQuantity;
            }
        }
        return stockLevels;
    }

    private async Task<StockDeductionResult> CheckAndDeductStockForOrder(Order order)
    {
        var insufficientItems = new List<InsufficientStockItem>();

        var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        var variantIds = order.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variants = await _context.ProductVariants.Where(v => variantIds.Contains(v.Id) && v.IsActive).ToDictionaryAsync(v => v.Id);

        var groups = order.Items.GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductVariantId,
                Quantity = group.Sum(item => item.Quantity),
                ProductName = group.First().ProductName
            }).ToList();

        // First check grouped quantities so duplicate lines cannot bypass validation.
        foreach (var group in groups)
        {
            products.TryGetValue(group.ProductId, out var product);
            variants.TryGetValue(group.ProductVariantId ?? 0, out var variant);
            var available = group.ProductVariantId.HasValue ? variant?.StockQuantity : product?.StockQuantity;
            if (!available.HasValue || available.Value < group.Quantity)
            {
                insufficientItems.Add(new InsufficientStockItem
                {
                    ProductId = group.ProductId,
                    ProductName = group.ProductName,
                    RequestedQuantity = group.Quantity,
                    AvailableQuantity = available ?? 0
                });
            }
        }

        // If all items have sufficient stock, deduct them
        var stockLevels = new Dictionary<(int ProductId, int? VariantId), int>();
        if (!insufficientItems.Any())
        {
            var isInMemory = (_context.Database.ProviderName ?? string.Empty).Contains("InMemory");
            foreach (var group in groups)
            {
                int rows;
                ProductVariant? variant = null;
                Product? product = null;
                if (group.ProductVariantId.HasValue && variants.TryGetValue(group.ProductVariantId.Value, out variant))
                {
                    if (isInMemory) { variant.StockQuantity -= group.Quantity; rows = 1; }
                    else rows = await _context.ProductVariants.Where(v => v.Id == variant.Id && v.IsActive &&
                            v.Product.IsActive && !v.Product.IsDeleted && v.StockQuantity >= group.Quantity)
                        .ExecuteUpdateAsync(update => update.SetProperty(v => v.StockQuantity, v => v.StockQuantity - group.Quantity));
                }
                else if (products.TryGetValue(group.ProductId, out product))
                {
                    if (isInMemory) { product.StockQuantity -= group.Quantity; rows = 1; }
                    else rows = await _context.Products.Where(p => p.Id == product.Id && p.IsActive && !p.IsDeleted &&
                            !p.Variants.Any(v => v.IsActive) && p.StockQuantity >= group.Quantity)
                        .ExecuteUpdateAsync(update => update.SetProperty(p => p.StockQuantity, p => p.StockQuantity - group.Quantity));
                }
                else rows = 0;

                if (rows == 0)
                {
                    insufficientItems.Add(new InsufficientStockItem
                    {
                        ProductId = group.ProductId, ProductName = group.ProductName,
                        RequestedQuantity = group.Quantity, AvailableQuantity = 0
                    });
                    break;
                }

                if (group.ProductVariantId.HasValue && variant != null)
                {
                    if (!isInMemory) SynchronizeStock(variant, variant.StockQuantity - group.Quantity);
                    stockLevels[(group.ProductId, group.ProductVariantId)] = variant.StockQuantity;
                }
                else if (product != null)
                {
                    if (!isInMemory) SynchronizeStock(product, product.StockQuantity - group.Quantity);
                    stockLevels[(group.ProductId, null)] = product.StockQuantity;
                }
            }
        }

        return new StockDeductionResult(insufficientItems, stockLevels);
    }

    private void SynchronizeStock(Product product, int stock)
    {
        product.StockQuantity = stock;
        var property = _context.Entry(product).Property(p => p.StockQuantity);
        property.OriginalValue = stock;
        property.IsModified = false;
    }

    private void SynchronizeStock(ProductVariant variant, int stock)
    {
        variant.StockQuantity = stock;
        var property = _context.Entry(variant).Property(v => v.StockQuantity);
        property.OriginalValue = stock;
        property.IsModified = false;
    }

    private sealed record StockDeductionResult(
        List<InsufficientStockItem> InsufficientItems,
        Dictionary<(int ProductId, int? VariantId), int> StockLevels);

    public async Task<OrderResult> UpdatePaymentStatusAsync(UpdatePaymentStatusRequest request)
    {
        try
        {
            var order = await _context.Orders.FindAsync(request.OrderId);

            if (order == null)
                return OrderResult.Fail(OrderErrorType.NotFound, "Đơn hàng không tồn tại");

            // Validate payment status transition
            if (!IsValidPaymentStatusTransition(order.PaymentStatus, request.NewPaymentStatus, order.Status))
                return OrderResult.Fail(OrderErrorType.InvalidPaymentStatusTransition,
                    $"Không thể chuyển trạng thái thanh toán từ {order.PaymentStatus} sang {request.NewPaymentStatus}");

            // Update payment status
            var oldPaymentStatus = order.PaymentStatus;
            order.PaymentStatus = request.NewPaymentStatus;
            await _context.SaveChangesAsync();

            // Log the change
            await _logService.LogPaymentStatusChangeAsync(order.Id, oldPaymentStatus, request.NewPaymentStatus, request.AdminId, request.Notes);

            // Realtime notification
            await _notifier.NotifyPaymentStatusChangedAsync(order.Id, order.UserId, order.PaymentStatus.ToString());

            return OrderResult.Ok(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrderResult.Fail(OrderErrorType.ConcurrencyConflict,
                "Đơn hàng đã được cập nhật bởi người dùng khác. Vui lòng tải lại dữ liệu.");
        }
    }

    public bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        // Cannot transition to same status
        if (currentStatus == newStatus)
            return false;

        // Define valid transitions as a state machine
        return (currentStatus, newStatus) switch
        {
            // From Pending
            (OrderStatus.Pending, OrderStatus.Processing) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,

            // From Processing
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,

            // From Shipped
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.Shipped, OrderStatus.Cancelled) => true,

            // From Delivered - can cancel (for returns/refunds)
            (OrderStatus.Delivered, OrderStatus.Cancelled) => true,

            // From Cancelled - can restore to Processing
            (OrderStatus.Cancelled, OrderStatus.Processing) => true,

            // All other transitions are invalid
            _ => false
        };
    }

    public bool IsValidPaymentStatusTransition(PaymentStatus currentStatus, PaymentStatus newStatus, OrderStatus orderStatus)
    {
        // Cannot transition to same status
        if (currentStatus == newStatus)
            return false;

        // Refund is only allowed when order is Cancelled
        if (newStatus == PaymentStatus.Refunded && orderStatus != OrderStatus.Cancelled)
            return false;

        // Define valid payment transitions
        return (currentStatus, newStatus) switch
        {
            // From Pending
            (PaymentStatus.Pending, PaymentStatus.Paid) => true,

            // From Paid
            (PaymentStatus.Paid, PaymentStatus.Refunded) => orderStatus == OrderStatus.Cancelled,

            // All other transitions are invalid
            _ => false
        };
    }

    /// <summary>
    /// Validates if a state transition is valid according to the state transition matrix
    /// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5
    /// - 6.1: Delivered cannot go back to Pending, Processing, Shipped
    /// - 6.2: Cancelled is terminal - no transitions allowed
    /// - 6.3: Returned is terminal - no transitions allowed
    /// - 6.4: Shipped cannot go back to Pending, Processing
    /// - 6.5: Processing cannot go back to Pending
    /// </summary>
    public bool IsValidStateTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        return StateTransitionRules.IsValidTransition(currentStatus, newStatus);
    }

    /// <summary>
    /// Gets the state transition rule for a specific order status
    /// Requirements: 6.6
    /// </summary>
    public StateTransitionRule GetStateTransitionRule(OrderStatus currentStatus)
    {
        return StateTransitionRules.GetRule(currentStatus);
    }

    /// <summary>
    /// Gets the allowed transitions for a specific order status
    /// Requirements: 6.6
    /// </summary>
    public OrderStatus[] GetAllowedTransitions(OrderStatus currentStatus)
    {
        return StateTransitionRules.GetAllowedTransitions(currentStatus);
    }

    /// <summary>
    /// Validates if a status combination is valid according to business rules (legacy - uses COD rules)
    /// </summary>
    public bool IsValidStatusCombination(OrderStatus orderStatus, PaymentStatus paymentStatus)
    {
        return StatusCombinationRules.IsValidCombination(orderStatus, paymentStatus);
    }

    /// <summary>
    /// Gets the combination rule for a specific order status (legacy - uses COD rules)
    /// </summary>
    public StatusCombinationRule GetStatusCombinationRule(OrderStatus orderStatus)
    {
        return StatusCombinationRules.GetRule(orderStatus);
    }

    /// <summary>
    /// Gets the allowed payment statuses for a specific order status (legacy - uses COD rules)
    /// </summary>
    public PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus)
    {
        return StatusCombinationRules.GetAllowedPaymentStatuses(orderStatus);
    }

    /// <summary>
    /// Validates if a status combination is valid according to business rules (payment method aware)
    /// Requirements: 3.2, 3.3, 7.1, 7.2, 7.3
    /// - 3.2: COD orders have specific valid combinations
    /// - 3.3: Bank Transfer orders allow Paid before Delivered
    /// - 7.1: COD orders lock PaymentStatus to Pending for Pending/Processing/Shipped
    /// - 7.2: Bank Transfer orders allow Pending or Paid for Pending/Processing/Shipped
    /// - 7.3: Bank Transfer Cancelled orders only allow Pending or Refunded
    /// </summary>
    public bool IsValidStatusCombination(OrderStatus orderStatus, PaymentStatus paymentStatus, PaymentMethod paymentMethod)
    {
        return StatusCombinationRules.IsValidCombination(orderStatus, paymentStatus, paymentMethod);
    }

    /// <summary>
    /// Gets the combination rule for a specific order status and payment method
    /// Requirements: 7.1, 7.2, 7.3
    /// </summary>
    public StatusCombinationRule GetStatusCombinationRule(OrderStatus orderStatus, PaymentMethod paymentMethod)
    {
        return StatusCombinationRules.GetRule(orderStatus, paymentMethod);
    }

    /// <summary>
    /// Gets the allowed payment statuses for a specific order status and payment method
    /// Requirements: 7.1, 7.2, 7.3
    /// </summary>
    public PaymentStatus[] GetAllowedPaymentStatuses(OrderStatus orderStatus, PaymentMethod paymentMethod)
    {
        return StatusCombinationRules.GetAllowedPaymentStatuses(orderStatus, paymentMethod);
    }

    /// <summary>
    /// Updates both order status and payment status in a single transaction
    /// Requirements: 1.2, 3.1, 3.4, 9.1
    /// - 1.2: Combined update in a single transaction
    /// - 3.1: Validate status combination according to payment method
    /// - 3.4: Validation failure does not change database
    /// - 9.1: Create audit log entry with complete information
    /// </summary>
    /// <param name="request">The update request containing new statuses and notes</param>
    /// <param name="adminId">The ID of the admin performing the update</param>
    /// <returns>OrderResult indicating success or failure</returns>
    public async Task<OrderResult> UpdateCombinedStatusAsync(UpdateCombinedStatusRequest request, int adminId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Get order with items
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId);

            if (order == null)
            {
                return OrderResult.Fail(OrderErrorType.NotFound, "Đơn hàng không tồn tại");
            }

            var oldOrderStatus = order.Status;
            var oldPaymentStatus = order.PaymentStatus;

            // Validate state transition (Requirements: 6.1, 6.2, 6.3, 6.4, 6.5)
            if (!IsValidStateTransition(oldOrderStatus, request.NewOrderStatus))
            {
                return OrderResult.Fail(OrderErrorType.InvalidStatusTransition,
                    $"Không thể chuyển từ trạng thái {oldOrderStatus} sang {request.NewOrderStatus}");
            }

            // Validate status combination with payment method awareness (Requirements: 3.2, 3.3, 7.1, 7.2, 7.3)
            if (!IsValidStatusCombination(request.NewOrderStatus, request.NewPaymentStatus, order.PaymentMethod))
            {
                return OrderResult.Fail(OrderErrorType.ValidationError,
                    $"Tổ hợp trạng thái không hợp lệ: {request.NewOrderStatus} + {request.NewPaymentStatus} cho phương thức thanh toán {order.PaymentMethod}");
            }

            // Handle stock management for order status changes
            Dictionary<(int ProductId, int? VariantId), int>? changedStocks = null;
            if (request.NewOrderStatus == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
            {
                // Restore stock when cancelling
                changedStocks = await RestoreStockForOrder(order);
            }
            else if (request.NewOrderStatus == OrderStatus.Returned && order.Status != OrderStatus.Returned)
            {
                // Restore stock when returning (if not already cancelled)
                if (order.Status != OrderStatus.Cancelled)
                {
                    changedStocks = await RestoreStockForOrder(order);
                }
            }
            else if (order.Status == OrderStatus.Cancelled && request.NewOrderStatus != OrderStatus.Cancelled)
            {
                // Deduct stock when restoring from cancelled
                var deduction = await CheckAndDeductStockForOrder(order);
                if (deduction.InsufficientItems.Any())
                {
                    await transaction.RollbackAsync();
                    return OrderResult.FailWithInsufficientStock(deduction.InsufficientItems);
                }
                changedStocks = deduction.StockLevels;
            }

            // Update both statuses atomically (Requirement 1.2)
            order.Status = request.NewOrderStatus;
            order.PaymentStatus = request.NewPaymentStatus;

            await _context.SaveChangesAsync();

            // Also log to existing OrderStatusHistory for backward compatibility
            if (oldOrderStatus != request.NewOrderStatus)
            {
                await _logService.LogStatusChangeAsync(order.Id, oldOrderStatus, request.NewOrderStatus, adminId, request.Notes);
            }

            if (oldPaymentStatus != request.NewPaymentStatus)
            {
                await _logService.LogPaymentStatusChangeAsync(order.Id, oldPaymentStatus, request.NewPaymentStatus, adminId, request.Notes);
            }

            await transaction.CommitAsync();

            // Realtime notification
            if (oldOrderStatus != request.NewOrderStatus)
            {
                await _notifier.NotifyOrderUpdatedAsync(order.Id, order.UserId, order.Status.ToString());
                // Handle stock broadcast
                if (request.NewOrderStatus == OrderStatus.Cancelled || request.NewOrderStatus == OrderStatus.Returned || 
                    (oldOrderStatus == OrderStatus.Cancelled && request.NewOrderStatus != OrderStatus.Cancelled))
                {
                    await NotifyStockForItemsAsync(order.Items, changedStocks);
                }
            }
            if (oldPaymentStatus != request.NewPaymentStatus)
            {
                await _notifier.NotifyPaymentStatusChangedAsync(order.Id, order.UserId, order.PaymentStatus.ToString());
            }

            return OrderResult.Ok(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return OrderResult.Fail(OrderErrorType.ConcurrencyConflict,
                "Đơn hàng đã được cập nhật bởi người dùng khác. Vui lòng tải lại dữ liệu.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<OrderNote>> GetOrderNotesAsync(int orderId)
    {
        return await _context.OrderNotes
            .Where(n => n.OrderId == orderId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<OrderNote> AddOrderNoteAsync(int orderId, string content, int adminId, string adminName)
    {
        var note = new OrderNote
        {
            OrderId = orderId,
            AdminId = adminId,
            AdminName = adminName,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        _context.OrderNotes.Add(note);
        await _context.SaveChangesAsync();

        await _notifier.NotifyOrderNoteAddedAsync(orderId, content);

        return note;
    }

    public async Task<bool> DeleteOrderNoteAsync(int noteId, int adminId)
    {
        var note = await _context.OrderNotes.FindAsync(noteId);
        if (note == null || note.AdminId != adminId)
            return false;
        _context.OrderNotes.Remove(note);
        await _context.SaveChangesAsync();
        return true;
    }
}
