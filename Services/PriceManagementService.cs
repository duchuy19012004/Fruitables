using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services;

public sealed class PriceManagementService : IPriceManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IRealtimeNotifier? _notifier;
    private readonly IIndexingService? _indexing;
    private readonly ILogger<PriceManagementService>? _logger;

    public PriceManagementService(IUnitOfWork unitOfWork, TimeProvider timeProvider,
        IRealtimeNotifier? notifier = null, IIndexingService? indexing = null,
        ILogger<PriceManagementService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _indexing = indexing;
        _logger = logger;
    }

    public async Task<PriceManagementViewModel> GetDashboardAsync(PriceDashboardQuery q)
    {
        var now = _timeProvider.GetUtcNow();
        IQueryable<Product> query = _unitOfWork.Products.Query()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Variants).ThenInclude(v => v.PriceSchedules)
            .Include(p => p.PriceSchedules);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.Name.Contains(q.Search) || p.Variants.Any(v => v.SKU.Contains(q.Search)));

        var products = await query.ToListAsync();
        var rows = new List<PriceManagementRow>();
        foreach (var product in products)
        {
            var activeVariants = product.Variants.Where(v => v.IsActive).OrderBy(v => v.Name).ToList();
            if (activeVariants.Count == 0)
            {
                rows.Add(BuildRow(product, null, product.Price, product.StockQuantity, product.PriceSchedules, now));
                continue;
            }

            foreach (var variant in activeVariants)
                rows.Add(BuildRow(product, variant, variant.Price, variant.StockQuantity, variant.PriceSchedules, now));
        }
        var statTotal = rows.Count;
        var statActive = rows.Count(row => row.CurrentSchedule != null);
        var statUpcoming = rows.Count(row => row.UpcomingSchedule != null);
        var statRegular = rows.Count(row => row.CurrentSchedule == null && row.UpcomingSchedule == null);

        rows = q.Filter switch
        {
            "active" => rows.Where(row => row.CurrentSchedule != null).ToList(),
            "upcoming" => rows.Where(row => row.UpcomingSchedule != null).ToList(),
            "regular" => rows.Where(row => row.CurrentSchedule == null && row.UpcomingSchedule == null).ToList(),
            _ => rows
        };

        // Nhóm theo sản phẩm (1 nhóm = sản phẩm + mọi biến thể), sort và phân trang theo nhóm
        // để một nhóm không bao giờ bị tách qua 2 trang.
        var groups = rows.GroupBy(r => r.ProductId).Select(g => g.ToList()).ToList();
        var desc = string.Equals(q.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        // Lưu ý: sort tên theo Ordinal — có thể khác nhẹ so với collation tiếng Việt của client cũ.
        IEnumerable<List<PriceManagementRow>> ordered = q.Sort switch
        {
            "base" => desc
                ? groups.OrderByDescending(g => g.Min(r => r.BasePrice))
                : groups.OrderBy(g => g.Min(r => r.BasePrice)),
            "effective" => desc
                ? groups.OrderByDescending(g => g.Min(r => r.EffectivePrice))
                : groups.OrderBy(g => g.Min(r => r.EffectivePrice)),
            _ => desc
                ? groups.OrderByDescending(g => g[0].ProductName, StringComparer.OrdinalIgnoreCase)
                : groups.OrderBy(g => g[0].ProductName, StringComparer.OrdinalIgnoreCase)
        };
        var totalGroups = groups.Count;
        var page = Math.Max(1, q.Page);
        var pagedRows = ordered
            .Skip((page - 1) * q.PageSize)
            .Take(q.PageSize)
            .SelectMany(g => g)
            .ToList();

        // Combobox đối tượng lịch: mọi sản phẩm/biến thể (không phụ thuộc trang/lọc hiện tại)
        var targetProducts = await _unitOfWork.Products.Query()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Variants)
            .OrderBy(p => p.Name)
            .ToListAsync();
        var targets = new List<ScheduleTargetItem>();
        foreach (var p in targetProducts)
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Name).ToList();
            if (activeVariants.Count == 0)
            {
                targets.Add(new ScheduleTargetItem { ProductId = p.Id, ProductName = p.Name, BasePrice = p.Price });
                continue;
            }
            foreach (var v in activeVariants)
                targets.Add(new ScheduleTargetItem
                {
                    ProductId = p.Id, ProductVariantId = v.Id, ProductName = p.Name,
                    VariantName = v.Name, SKU = v.SKU, BasePrice = v.Price
                });
        }

        // Tab Lịch giảm giá: lọc trạng thái + tìm kiếm + phân trang ở SQL.
        IQueryable<PriceSchedule> schQuery = _unitOfWork.PriceSchedules.Query()
            .Include(s => s.Product).Include(s => s.ProductVariant);
        if (!string.IsNullOrWhiteSpace(q.ScheduleSearch))
            schQuery = schQuery.Where(s => s.Product.Name.Contains(q.ScheduleSearch) ||
                (s.ProductVariant != null &&
                    (s.ProductVariant.Name.Contains(q.ScheduleSearch) || s.ProductVariant.SKU.Contains(q.ScheduleSearch))));

        // Đếm theo trạng thái (1 query projection, đếm in-memory). Thứ tự ưu tiên trạng thái
        // PHẢI khớp PriceSchedule.GetStatus: cancelled → scheduled → ended → active.
        var statusFacts = await schQuery
            .Select(s => new { s.IsCancelled, s.StartsAt, s.EndsAt })
            .ToListAsync();
        var statusCounts = new Dictionary<string, int>
        {
            ["all"] = statusFacts.Count, ["active"] = 0, ["scheduled"] = 0, ["ended"] = 0, ["cancelled"] = 0
        };
        foreach (var s in statusFacts)
        {
            var key = s.IsCancelled ? "cancelled"
                : s.StartsAt > now ? "scheduled"
                : s.EndsAt.HasValue && s.EndsAt.Value <= now ? "ended"
                : "active";
            statusCounts[key]++;
        }

        // Mirror SQL của PriceSchedule.GetStatus — giữ đồng bộ khi sửa model.
        schQuery = q.ScheduleStatus switch
        {
            "active" => schQuery.Where(s => !s.IsCancelled && s.StartsAt <= now && (!s.EndsAt.HasValue || s.EndsAt > now)),
            "scheduled" => schQuery.Where(s => !s.IsCancelled && s.StartsAt > now),
            "ended" => schQuery.Where(s => !s.IsCancelled && s.EndsAt.HasValue && s.EndsAt <= now),
            "cancelled" => schQuery.Where(s => s.IsCancelled),
            _ => schQuery
        };
        var schTotal = await schQuery.CountAsync();
        var schPage = Math.Max(1, q.SchedulePage);
        var schItems = await schQuery
            .OrderByDescending(s => s.StartsAt)
            .Skip((schPage - 1) * q.SchedulePageSize)
            .Take(q.SchedulePageSize)
            .ToListAsync();

        return new PriceManagementViewModel
        {
            Search = q.Search,
            Filter = q.Filter,
            Rows = pagedRows,
            StatTotal = statTotal,
            StatActive = statActive,
            StatUpcoming = statUpcoming,
            StatRegular = statRegular,
            Tab = q.Tab,
            Sort = q.Sort,
            Dir = q.Dir,
            Page = page,
            PageSize = q.PageSize,
            TotalGroups = totalGroups,
            ScheduleTargets = targets,
            ScheduleStatus = q.ScheduleStatus,
            ScheduleSearch = q.ScheduleSearch,
            SchedulesPage = new PagedResult<PriceSchedule>
            {
                Items = schItems,
                TotalCount = schTotal,
                Page = schPage,
                PageSize = q.SchedulePageSize
            },
            ScheduleStatusCounts = statusCounts
        };
    }

    public async Task<PriceManagementResult> CreateScheduleAsync(SavePriceScheduleRequest request, int adminId)
    {
        var result = await RunSerializedWriteAsync(async () =>
        {
            var validation = await ValidateScheduleAsync(request, null);
            if (validation != null) return PriceManagementResult.Fail(validation);

            var schedule = new PriceSchedule
            {
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                DiscountType = request.DiscountType,
                Value = request.Value,
                StartsAt = request.StartsAt.ToUniversalTime(),
                EndsAt = request.EndsAt?.ToUniversalTime(),
                CreatedByAdminId = adminId,
                CreatedAt = _timeProvider.GetUtcNow(),
                UpdatedAt = _timeProvider.GetUtcNow()
            };
            await _unitOfWork.PriceSchedules.AddAsync(schedule);
            await AddLogAsync(request.ProductId, adminId, "PriceScheduleCreate", $"Tạo lịch giảm giá từ {schedule.StartsAt:O}");
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok();
        });
        if (result.Success) await PublishPriceChangeAsync(request.ProductId, request.ProductVariantId);
        return result;
    }

    public async Task<PriceManagementResult> UpdateScheduleAsync(int id, SavePriceScheduleRequest request, int adminId)
    {
        var result = await RunSerializedWriteAsync(async () =>
        {
            var schedule = await _unitOfWork.PriceSchedules.GetByIdAsync(id);
            if (schedule == null) return PriceManagementResult.Fail("Không tìm thấy lịch giá.");
            if (schedule.GetStatus(_timeProvider.GetUtcNow()) != PriceScheduleStatus.Scheduled)
                return PriceManagementResult.Fail("Chỉ lịch chưa bắt đầu mới được chỉnh sửa.");
            if (schedule.ProductId != request.ProductId || schedule.ProductVariantId != request.ProductVariantId)
                return PriceManagementResult.Fail("Không thể đổi đối tượng của một lịch giá đã tạo.");

            var validation = await ValidateScheduleAsync(request, id);
            if (validation != null) return PriceManagementResult.Fail(validation);
            schedule.DiscountType = request.DiscountType;
            schedule.Value = request.Value;
            schedule.StartsAt = request.StartsAt.ToUniversalTime();
            schedule.EndsAt = request.EndsAt?.ToUniversalTime();
            schedule.UpdatedAt = _timeProvider.GetUtcNow();
            await AddLogAsync(request.ProductId, adminId, "PriceScheduleUpdate", $"Cập nhật lịch giá #{id}");
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok();
        });
        if (result.Success) await PublishPriceChangeAsync(request.ProductId, request.ProductVariantId);
        return result;
    }

    public async Task<PriceManagementResult> CancelScheduleAsync(int id, int adminId)
    {
        var schedule = await _unitOfWork.PriceSchedules.GetByIdAsync(id);
        if (schedule == null) return PriceManagementResult.Fail("Không tìm thấy lịch giá.");
        var status = schedule.GetStatus(_timeProvider.GetUtcNow());
        if (status is PriceScheduleStatus.Ended or PriceScheduleStatus.Cancelled)
            return PriceManagementResult.Fail("Lịch đã kết thúc hoặc đã hủy.");
        schedule.IsCancelled = true;
        schedule.UpdatedAt = _timeProvider.GetUtcNow();
        await AddLogAsync(schedule.ProductId, adminId, "PriceScheduleCancel", $"Hủy lịch giá #{id}");
        await _unitOfWork.SaveChangesAsync();
        await PublishPriceChangeAsync(schedule.ProductId, schedule.ProductVariantId);
        return PriceManagementResult.Ok();
    }

    public async Task<PriceManagementResult> UpdateBasePriceAsync(PriceTargetKey target, decimal newPrice, int adminId)
    {
        var result = await RunSerializedWriteAsync(async () =>
        {
            var validation = await ValidateNewBasePriceAsync(target, newPrice);
            if (validation != null) return PriceManagementResult.Fail(validation);
            if (target.ProductVariantId.HasValue)
                (await _unitOfWork.ProductVariants.GetByIdAsync(target.ProductVariantId.Value))!.Price = newPrice;
            else
                (await _unitOfWork.Products.GetByIdAsync(target.ProductId))!.Price = newPrice;
            await AddLogAsync(target.ProductId, adminId, "BasePriceUpdate", $"Cập nhật giá gốc thành {newPrice:N0}đ");
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok();
        });
        if (result.Success) await PublishPriceChangeAsync(target.ProductId, target.ProductVariantId);
        return result;
    }

    public async Task<PriceManagementResult> BulkUpdateBasePricesAsync(BulkPriceUpdateRequest request, int adminId)
    {
        if (request.Targets.Count == 0 || request.Value <= 0)
            return PriceManagementResult.Fail("Vui lòng chọn đối tượng và nhập mức điều chỉnh lớn hơn 0.");

        var changes = new List<(PriceTargetKey Target, decimal NewPrice)>();
        var result = await RunSerializedWriteAsync(async () =>
        {
            foreach (var target in request.Targets.Distinct())
            {
                var current = await GetBasePriceAsync(target);
                if (!current.HasValue) return PriceManagementResult.Fail("Có sản phẩm hoặc biến thể không tồn tại.");
                var delta = request.AdjustmentType == PriceAdjustmentType.Percentage
                    ? Math.Round(current.Value * request.Value / 100m, 0, MidpointRounding.AwayFromZero)
                    : request.Value;
                var next = request.Direction == PriceAdjustmentDirection.Increase ? current.Value + delta : current.Value - delta;
                var validation = await ValidateNewBasePriceAsync(target, next);
                if (validation != null) return PriceManagementResult.Fail(validation);
                changes.Add((target, next));
            }

            foreach (var change in changes)
            {
                if (change.Target.ProductVariantId.HasValue)
                    (await _unitOfWork.ProductVariants.GetByIdAsync(change.Target.ProductVariantId.Value))!.Price = change.NewPrice;
                else
                    (await _unitOfWork.Products.GetByIdAsync(change.Target.ProductId))!.Price = change.NewPrice;
                await AddLogAsync(change.Target.ProductId, adminId, "BulkPriceUpdate", $"Cập nhật hàng loạt thành {change.NewPrice:N0}đ");
            }
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok();
        });
        if (!result.Success) return result;
        foreach (var target in changes.Select(c => c.Target).Distinct())
            await PublishPriceChangeAsync(target.ProductId, target.ProductVariantId);
        return PriceManagementResult.Ok();
    }

    private async Task<PriceManagementResult> RunSerializedWriteAsync(Func<Task<PriceManagementResult>> action)
    {
        if ((_unitOfWork.DatabaseProviderName ?? string.Empty).Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return await action();

        await using var transaction = await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var result = await action();
            if (result.Success) await transaction.CommitAsync();
            else await transaction.RollbackAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> ValidateScheduleAsync(SavePriceScheduleRequest request, int? excludingId)
    {
        var product = await _unitOfWork.Products.Query().Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null) return "Không tìm thấy sản phẩm.";
        decimal basePrice;
        if (request.ProductVariantId.HasValue)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == request.ProductVariantId && v.IsActive);
            if (variant == null) return "Biến thể không hợp lệ hoặc đã ngừng hoạt động.";
            basePrice = variant.Price;
        }
        else
        {
            if (product.Variants.Any(v => v.IsActive)) return "Sản phẩm có biến thể phải đặt lịch cho từng biến thể.";
            basePrice = product.Price;
        }

        if (request.EndsAt.HasValue && request.StartsAt >= request.EndsAt.Value)
            return "Thời gian kết thúc phải sau thời gian bắt đầu.";
        if (!Enum.IsDefined(request.DiscountType))
            return "Kiểu giảm giá không hợp lệ.";
        if (request.DiscountType == DiscountType.FixedPrice && (request.Value < 0 || request.Value >= basePrice))
            return "Giá giảm cố định phải nhỏ hơn giá gốc và không được âm.";
        if (request.DiscountType == DiscountType.Percentage && (request.Value <= 0 || request.Value > 100))
            return "Phần trăm giảm phải lớn hơn 0 và không quá 100.";

        var start = request.StartsAt.ToUniversalTime();
        var end = request.EndsAt?.ToUniversalTime();
        var overlaps = await _unitOfWork.PriceSchedules.Query().AnyAsync(s =>
            s.Id != excludingId && !s.IsCancelled && s.ProductId == request.ProductId &&
            s.ProductVariantId == request.ProductVariantId &&
            (!s.EndsAt.HasValue || start < s.EndsAt.Value) && (!end.HasValue || s.StartsAt < end.Value));
        return overlaps ? "Khoảng thời gian bị trùng với một lịch giá khác." : null;
    }

    private async Task<string?> ValidateNewBasePriceAsync(PriceTargetKey target, decimal newPrice)
    {
        if (newPrice <= 0 || newPrice > 99_999_999.99m) return "Giá gốc mới không hợp lệ.";
        if (!(await GetBasePriceAsync(target)).HasValue) return "Không tìm thấy sản phẩm hoặc biến thể.";
        var now = _timeProvider.GetUtcNow();
        var invalidFixed = await _unitOfWork.PriceSchedules.Query().AnyAsync(s =>
            s.ProductId == target.ProductId && s.ProductVariantId == target.ProductVariantId && !s.IsCancelled &&
            (!s.EndsAt.HasValue || s.EndsAt > now) && s.DiscountType == DiscountType.FixedPrice && s.Value >= newPrice);
        return invalidFixed ? "Giá mới làm lịch giảm giá cố định đang chạy hoặc sắp tới không còn hợp lệ." : null;
    }

    private async Task<decimal?> GetBasePriceAsync(PriceTargetKey target)
    {
        if (target.ProductVariantId.HasValue)
            return await _unitOfWork.ProductVariants.Query()
                .Where(v => v.Id == target.ProductVariantId && v.ProductId == target.ProductId)
                .Select(v => (decimal?)v.Price).FirstOrDefaultAsync();
        return await _unitOfWork.Products.Query().Where(p => p.Id == target.ProductId)
            .Select(p => (decimal?)p.Price).FirstOrDefaultAsync();
    }

    private async Task AddLogAsync(int productId, int adminId, string action, string details) =>
        await _unitOfWork.ProductLogs.AddAsync(new ProductLog
        {
            ProductId = productId, AdminId = adminId, Action = action, Details = details,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

    private async Task PublishPriceChangeAsync(int productId, int? variantId)
    {
        try
        {
            if (_notifier != null) await _notifier.NotifyPriceChangedAsync(productId, variantId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Broadcast price change failed for product {ProductId}", productId);
        }
        try
        {
            if (_indexing != null) await _indexing.IndexProductAsync(productId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Reindex price change failed for product {ProductId}", productId);
        }
    }

    private static PriceManagementRow BuildRow(Product product, ProductVariant? variant, decimal basePrice, int stock,
        IEnumerable<PriceSchedule> schedules, DateTimeOffset now)
    {
        var list = schedules.Where(s => !s.IsCancelled).ToList();
        var quote = ProductPricingService.CalculateQuote(basePrice, list, now);
        return new PriceManagementRow
        {
            ProductId = product.Id, ProductVariantId = variant?.Id, ProductName = product.Name,
            VariantName = variant?.Name, SKU = variant?.SKU, BasePrice = basePrice,
            EffectivePrice = quote.EffectivePrice, StockQuantity = stock,
            CurrentSchedule = list.FirstOrDefault(s => s.IsActiveAt(now)),
            UpcomingSchedule = list.Where(s => s.StartsAt > now).OrderBy(s => s.StartsAt).FirstOrDefault(),
            Schedules = schedules.OrderBy(s => s.StartsAt).ToList()
        };
    }
}
