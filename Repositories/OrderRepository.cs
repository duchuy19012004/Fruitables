using Microsoft.EntityFrameworkCore;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Catalog.Products;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Orders.OrderManagement;
using Fruitables.ViewModels;

namespace Fruitables.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    private readonly IJsonDocumentSerializer _serializer;

    public OrderRepository(ApplicationDbContext context, IJsonDocumentSerializer? serializer = null) : base(context)
    {
        _serializer = serializer ?? new VersionedJsonSerializer();
    }

    /// <summary>
    /// Lấy danh sách đơn hàng của khách hàng với phân trang và lọc
    /// </summary>
    public async Task<PagedResult<Order>> GetOrdersByUserIdAsync(int userId, OrderHistoryFilter filter)
    {
        var query = _dbSet
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .AsQueryable();

        // Áp dụng bộ lọc tìm kiếm theo mã đơn hàng
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            query = query.Where(o => o.OrderNumber.Contains(filter.SearchTerm));
        }

        // Áp dụng bộ lọc theo trạng thái
        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        // Áp dụng bộ lọc theo ngày
        if (filter.FromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= filter.ToDate.Value);
        }

        // Sắp xếp theo thời gian tạo mới nhất
        query = query.OrderByDescending(o => o.CreatedAt);

        // Đếm tổng số bản ghi
        var totalCount = await query.CountAsync();

        // Áp dụng phân trang
        var orders = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Order>
        {
            Items = orders,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    /// <summary>
    /// Lấy đơn hàng với đầy đủ thông tin chi tiết
    /// </summary>
    public async Task<Order?> GetOrderWithDetailsAsync(int orderId, int userId)
    {
        var order = await _dbSet
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.Items)
            .ThenInclude(oi => oi.ProductVariant)
            .Include(o => o.Address)
            .FirstOrDefaultAsync();
        if (order == null)
            return null;

        var products = order.Items
            .Where(item => item.Product != null)
            .Select(item => item.Product!)
            .GroupBy(product => product.Id)
            .Select(group => group.First())
            .ToList();
        ProductAggregateJson.Hydrate(products, _serializer);

        var history = OrderAggregateJson.ReadHistory(order.StatusHistoryJson, _serializer);
        var adminIds = history.Entries.Select(entry => entry.AdminId).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<int, User>()
            : await _context.Users.Where(user => adminIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id);
        order.StatusHistory = OrderAggregateJson.ToHistoryEntities(order.Id, history, admins);
        return order;
    }

    /// <summary>
    /// Lấy lịch sử thay đổi trạng thái của đơn hàng
    /// </summary>
    public async Task<List<OrderStatusHistory>> GetOrderStatusHistoryAsync(int orderId)
    {
        var order = await _dbSet.AsNoTracking().FirstOrDefaultAsync(item => item.Id == orderId);
        if (order == null)
            return [];

        var history = OrderAggregateJson.ReadHistory(order.StatusHistoryJson, _serializer);
        var adminIds = history.Entries.Select(entry => entry.AdminId).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<int, User>()
            : await _context.Users.AsNoTracking().Where(user => adminIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id);
        return OrderAggregateJson.ToHistoryEntities(order.Id, history, admins)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Kiểm tra xem đơn hàng có thuộc về user không
    /// </summary>
    public async Task<bool> IsOrderOwnedByUserAsync(int orderId, int userId)
    {
        return await _dbSet.AnyAsync(o => o.Id == orderId && o.UserId == userId);
    }

    /// <summary>
    /// Lấy số lượng đơn hàng theo trạng thái của user
    /// </summary>
    public async Task<Dictionary<OrderStatus, int>> GetOrderCountByStatusAsync(int userId)
    {
        var counts = await _dbSet
            .Where(o => o.UserId == userId)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    /// <summary>
    /// Hủy đơn hàng và hoàn trả stock trong một transaction
    /// </summary>
    public async Task<StockRestoreResult> CancelOrderWithStockRestoreAsync(int orderId, string cancelReason, int? userId = null)
    {
        // Check if database supports transactions (InMemory doesn't)
        var providerName = _context.Database.ProviderName ?? "";
        var supportsTransactions = !providerName.Contains("InMemory");
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        
        if (supportsTransactions)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }
        
        try
        {
            // 1. Get order with items
            var order = await _dbSet
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return StockRestoreResult.Fail("Đơn hàng không tồn tại");
            }

            if (order.Status != OrderStatus.Pending)
            {
                return StockRestoreResult.Fail("Chỉ có thể hủy đơn hàng ở trạng thái Chờ xử lý");
            }

            // 2. Update order status
            var oldStatus = order.Status;
            order.Status = OrderStatus.Cancelled;
            order.CancelReason = cancelReason;

            // 3. Restore stock for each product in batch
            var restoredItems = new List<StockRestoreItem>();
            var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var variantIds = order.Items.Where(item => item.ProductVariantId.HasValue)
                .Select(item => item.ProductVariantId!.Value).Distinct().ToList();
            var variants = await _context.ProductVariants
                .Where(variant => variantIds.Contains(variant.Id))
                .ToDictionaryAsync(variant => variant.Id);

            var restoreGroups = order.Items
                .GroupBy(item => new { item.ProductId, item.ProductVariantId })
                .Select(group => new { group.Key.ProductId, group.Key.ProductVariantId, Quantity = group.Sum(item => item.Quantity), Item = group.First() })
                .ToList();
            foreach (var group in restoreGroups)
            {
                if (group.ProductVariantId.HasValue && variants.TryGetValue(group.ProductVariantId.Value, out var variant))
                {
                    if (supportsTransactions)
                    {
                        var rows = await _context.ProductVariants.Where(v => v.Id == variant.Id)
                            .ExecuteUpdateAsync(update => update.SetProperty(v => v.StockQuantity, v => v.StockQuantity + group.Quantity));
                        if (rows == 0) return StockRestoreResult.Fail("Biến thể trong đơn hàng không còn tồn tại");
                    }
                    else variant.StockQuantity += group.Quantity;
                    restoredItems.Add(new StockRestoreItem
                    {
                        ProductId = group.ProductId,
                        ProductVariantId = group.ProductVariantId,
                        ProductName = $"{group.Item.ProductName} · {group.Item.VariantName ?? variant.Name}",
                        QuantityRestored = group.Quantity,
                        CurrentStock = variant.StockQuantity
                    });
                }
                else if (products.TryGetValue(group.ProductId, out var product))
                {
                    if (supportsTransactions)
                    {
                        var rows = await _context.Products.Where(p => p.Id == product.Id)
                            .ExecuteUpdateAsync(update => update.SetProperty(p => p.StockQuantity, p => p.StockQuantity + group.Quantity));
                        if (rows == 0) return StockRestoreResult.Fail("Sản phẩm trong đơn hàng không còn tồn tại");
                    }
                    else product.StockQuantity += group.Quantity;
                    restoredItems.Add(new StockRestoreItem
                    {
                        ProductId = group.ProductId,
                        ProductName = product.Name,
                        QuantityRestored = group.Quantity,
                        CurrentStock = product.StockQuantity
                    });
                }
            }

            // ExecuteUpdate bypasses tracking. Read the committed candidates in
            // two batches while still inside the transaction so realtime receives
            // the exact post-restore quantities, including concurrent-safe updates.
            if (supportsTransactions)
            {
                var currentProductStocks = await _context.Products.AsNoTracking()
                    .Where(product => productIds.Contains(product.Id))
                    .ToDictionaryAsync(product => product.Id, product => product.StockQuantity);
                var currentVariantStocks = await _context.ProductVariants.AsNoTracking()
                    .Where(variant => variantIds.Contains(variant.Id))
                    .ToDictionaryAsync(variant => variant.Id, variant => variant.StockQuantity);
                foreach (var item in restoredItems)
                {
                    if (item.ProductVariantId.HasValue && currentVariantStocks.TryGetValue(item.ProductVariantId.Value, out var variantStock))
                        item.CurrentStock = variantStock;
                    else if (currentProductStocks.TryGetValue(item.ProductId, out var productStock))
                        item.CurrentStock = productStock;
                }
            }

            // 4. Create status history with stock info
            var stockInfo = string.Join(", ", restoredItems.Select(r => $"{r.ProductName}: +{r.QuantityRestored}"));
            var notes = $"{cancelReason}. Hoàn trả stock: {stockInfo}";

            var history = OrderAggregateJson.ReadHistory(order.StatusHistoryJson, _serializer);
            order.StatusHistoryJson = OrderAggregateJson.SerializeHistory(
                OrderAggregateJson.AppendHistory(
                    history,
                    oldStatus,
                    OrderStatus.Cancelled,
                    userId ?? order.UserId ?? 1,
                    notes,
                    DateTime.UtcNow),
                _serializer);

            // 5. Save and commit
            await _context.SaveChangesAsync();
            
            if (transaction != null)
            {
                await transaction.CommitAsync();
                // ExecuteUpdate bypasses tracked stock entities. Detach after commit so
                // the caller's detail query observes the committed quantities.
                foreach (var product in products.Values) _context.Entry(product).State = EntityState.Detached;
                foreach (var variant in variants.Values) _context.Entry(variant).State = EntityState.Detached;
            }

            return StockRestoreResult.Success(restoredItems);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            return StockRestoreResult.Fail($"Lỗi khi hủy đơn hàng: {ex.Message}");
        }
        finally
        {
            transaction?.Dispose();
        }
    }
}
