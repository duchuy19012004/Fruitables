using Fruitables.Models;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Pricing;
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
                rows.Add(BuildRow(product, null, product.Price, product.PriceRevision, product.StockQuantity, product.PriceSchedules, now));
                continue;
            }

            foreach (var variant in activeVariants)
                rows.Add(BuildRow(product, variant, variant.Price, variant.PriceRevision, variant.StockQuantity, variant.PriceSchedules, now));
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
                targets.Add(new ScheduleTargetItem { ProductId = p.Id, ProductName = p.Name, BasePrice = p.Price, PriceRevision = p.PriceRevision });
                continue;
            }
            foreach (var v in activeVariants)
                targets.Add(new ScheduleTargetItem
                {
                    ProductId = p.Id, ProductVariantId = v.Id, ProductName = p.Name,
                    VariantName = v.Name, SKU = v.SKU, BasePrice = v.Price, PriceRevision = v.PriceRevision
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
            .Select(s => new { s.IsCancelled, s.CancelledAt, s.StartsAt, s.EndsAt })
            .ToListAsync();
        var statusCounts = new Dictionary<string, int>
        {
            ["all"] = statusFacts.Count,
            ["active"] = 0,
            ["scheduled"] = 0,
            ["ended"] = 0,
            ["cancelled"] = 0,
            ["stopped"] = 0
        };
        foreach (var s in statusFacts)
        {
            var key = s.IsCancelled
                ? s.CancelledAt.HasValue && s.CancelledAt.Value > s.StartsAt ? "stopped" : "cancelled"
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
            "stopped" => schQuery.Where(s =>
                s.IsCancelled &&
                s.CancelledAt.HasValue &&
                s.CancelledAt.Value > s.StartsAt),
            "cancelled" => schQuery.Where(s =>
                s.IsCancelled &&
                (!s.CancelledAt.HasValue || s.CancelledAt.Value <= s.StartsAt)),
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
            if (request.ExpectedRevision <= 0 || schedule.Revision != request.ExpectedRevision)
                return PriceManagementResult.Fail("Lịch giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");
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
            schedule.Revision++;
            await AddLogAsync(request.ProductId, adminId, "PriceScheduleUpdate", $"Cập nhật lịch giá #{id}");
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok(schedule.Revision);
        });
        if (result.Success) await PublishPriceChangeAsync(request.ProductId, request.ProductVariantId);
        return result;
    }

    public async Task<PriceManagementResult> CancelScheduleAsync(
        int id,
        CancelPriceScheduleRequest request,
        int adminId)
    {
        int productId = 0;
        int? variantId = null;

        var result = await RunSerializedWriteAsync(async () =>
        {
            var schedule = await _unitOfWork.PriceSchedules.GetByIdAsync(id);
            if (schedule == null)
                return PriceManagementResult.Fail("Không tìm thấy lịch giá.");

            if (request.ExpectedRevision <= 0 || schedule.Revision != request.ExpectedRevision)
                return PriceManagementResult.Fail("Lịch giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");

            var status = schedule.GetStatus(_timeProvider.GetUtcNow());
            if (status is PriceScheduleStatus.Ended or PriceScheduleStatus.Cancelled or PriceScheduleStatus.StoppedEarly)
                return PriceManagementResult.Fail("Lịch đã kết thúc hoặc đã dừng.");

            var reason = request.Reason?.Trim();
            if (reason?.Length > 500)
                return PriceManagementResult.Fail("Lý do hủy không được vượt quá 500 ký tự.");

            var now = _timeProvider.GetUtcNow();
            schedule.IsCancelled = true;
            schedule.CancelledAt = now;
            schedule.CancelledByAdminId = adminId;
            schedule.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
            schedule.UpdatedAt = now;
            schedule.Revision++;

            productId = schedule.ProductId;
            variantId = schedule.ProductVariantId;

            var action = status == PriceScheduleStatus.Active
                ? "PriceScheduleStoppedEarly"
                : "PriceScheduleCancel";
            var detail = $"{action} #{id}; reason={schedule.CancellationReason ?? "không có"}; cancelledAt={now:O}";
            await AddLogAsync(schedule.ProductId, adminId, action, detail);
            await _unitOfWork.SaveChangesAsync();

            return PriceManagementResult.Ok(schedule.Revision);
        });

        if (result.Success)
            await PublishPriceChangeAsync(productId, variantId);

        return result;
    }

    private sealed record BasePriceSnapshot(decimal Price, int Revision);

    private async Task<BasePriceSnapshot?> GetBasePriceSnapshotAsync(PriceTargetKey target)
    {
        if (target.ProductVariantId.HasValue)
        {
            return await _unitOfWork.ProductVariants.Query()
                .Where(variant => variant.Id == target.ProductVariantId.Value && variant.ProductId == target.ProductId)
                .Select(variant => new BasePriceSnapshot(variant.Price, variant.PriceRevision))
                .FirstOrDefaultAsync();
        }

        return await _unitOfWork.Products.Query()
            .Where(product => product.Id == target.ProductId)
            .Select(product => new BasePriceSnapshot(product.Price, product.PriceRevision))
            .FirstOrDefaultAsync();
    }

    public async Task<PriceManagementResult> UpdateBasePriceAsync(
        UpdateBasePriceRequest request,
        int adminId)
    {
        var target = request.Target;
        var result = await RunSerializedWriteAsync(async () =>
        {
            var snapshot = await GetBasePriceSnapshotAsync(target);
            if (snapshot == null)
                return PriceManagementResult.Fail("Không tìm thấy sản phẩm hoặc biến thể.");

            if (snapshot.Price != request.ExpectedBasePrice || snapshot.Revision != request.ExpectedRevision)
                return PriceManagementResult.Fail("Giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");

            var validation = await ValidateNewBasePriceAsync(target, request.NewPrice);
            if (validation != null)
                return PriceManagementResult.Fail(validation);

            int newRevision;
            if (target.ProductVariantId.HasValue)
            {
                var variant = await _unitOfWork.ProductVariants.GetByIdAsync(target.ProductVariantId.Value);
                if (variant == null || variant.ProductId != target.ProductId)
                    return PriceManagementResult.Fail("Không tìm thấy sản phẩm hoặc biến thể.");

                variant.Price = request.NewPrice;
                variant.PriceRevision++;
                newRevision = variant.PriceRevision;
            }
            else
            {
                var product = await _unitOfWork.Products.GetByIdAsync(target.ProductId);
                if (product == null)
                    return PriceManagementResult.Fail("Không tìm thấy sản phẩm hoặc biến thể.");

                product.Price = request.NewPrice;
                product.PriceRevision++;
                newRevision = product.PriceRevision;
            }

            await AddLogAsync(
                target.ProductId,
                adminId,
                "BasePriceUpdate",
                $"Giá gốc {snapshot.Price:N0}đ -> {request.NewPrice:N0}đ; revision={snapshot.Revision}->{newRevision}");
            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok(newRevision);
        });

        if (result.Success)
            await PublishPriceChangeAsync(target.ProductId, target.ProductVariantId);

        return result;
    }

    public async Task<PriceManagementResult> BulkUpdateBasePricesAsync(BulkPriceUpdateRequest request, int adminId)
    {
        if (request.Targets.Count == 0 || request.Value <= 0)
        {
            return PriceManagementResult.Fail(
                "Vui lòng chọn đối tượng và nhập mức điều chỉnh lớn hơn 0.");
        }

        if (request.AdjustmentType == PriceAdjustmentType.Amount &&
            !VndPriceRules.IsValidFixedAdjustment(request.Value))
        {
            return PriceManagementResult.Fail(
                "Mức điều chỉnh theo số tiền phải là số nguyên dương theo đơn vị VNĐ.");
        }

        if (request.AdjustmentType == PriceAdjustmentType.Percentage &&
            (request.Value < 1 || request.Value > 99))
        {
            return PriceManagementResult.Fail(
                "Phần trăm điều chỉnh phải từ 1 đến 99.");
        }

        var duplicateTarget = request.Targets
            .GroupBy(item => item.Target)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTarget != null)
            return PriceManagementResult.Fail("Danh sách cập nhật có đối tượng bị trùng.");

        var changes = new List<(BulkPriceTargetRequest Request, decimal CurrentPrice, decimal NewPrice)>();

        var result = await RunSerializedWriteAsync(async () =>
        {
            foreach (var item in request.Targets)
            {
                var snapshot = await GetBasePriceSnapshotAsync(item.Target);
                if (snapshot == null)
                    return PriceManagementResult.Fail("Có sản phẩm hoặc biến thể không tồn tại.");

                if (snapshot.Price != item.ExpectedBasePrice || snapshot.Revision != item.ExpectedRevision)
                    return PriceManagementResult.Fail("Có giá đã thay đổi sau khi xem trước. Vui lòng tải lại và xem trước lại.");

                var delta = request.AdjustmentType == PriceAdjustmentType.Percentage
                    ? Math.Round(snapshot.Price * request.Value / 100m, 0, MidpointRounding.AwayFromZero)
                    : request.Value;
                var next = request.Direction == PriceAdjustmentDirection.Increase
                    ? snapshot.Price + delta
                    : snapshot.Price - delta;

                var validation = await ValidateNewBasePriceAsync(item.Target, next);
                if (validation != null)
                    return PriceManagementResult.Fail(validation);

                changes.Add((item, snapshot.Price, next));
            }

            foreach (var change in changes)
            {
                var target = change.Request.Target;
                int newRevision;

                if (target.ProductVariantId.HasValue)
                {
                    var variant = await _unitOfWork.ProductVariants.GetByIdAsync(target.ProductVariantId.Value);
                    if (variant == null || variant.ProductId != target.ProductId)
                        return PriceManagementResult.Fail("Có sản phẩm hoặc biến thể không tồn tại.");

                    variant.Price = change.NewPrice;
                    variant.PriceRevision++;
                    newRevision = variant.PriceRevision;
                }
                else
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(target.ProductId);
                    if (product == null)
                        return PriceManagementResult.Fail("Có sản phẩm hoặc biến thể không tồn tại.");

                    product.Price = change.NewPrice;
                    product.PriceRevision++;
                    newRevision = product.PriceRevision;
                }

                await AddLogAsync(
                    target.ProductId,
                    adminId,
                    "BulkPriceUpdate",
                    $"Giá gốc {change.CurrentPrice:N0}đ -> {change.NewPrice:N0}đ; revision={change.Request.ExpectedRevision}->{newRevision}");
            }

            await _unitOfWork.SaveChangesAsync();
            return PriceManagementResult.Ok();
        });

        if (!result.Success) return result;
        foreach (var target in changes.Select(c => c.Request.Target).Distinct())
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
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return PriceManagementResult.Fail("Dữ liệu giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");
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

        var now = _timeProvider.GetUtcNow();
        if (request.EndsAt.HasValue && request.EndsAt.Value.ToUniversalTime() <= now)
            return "Không thể tạo hoặc cập nhật một lịch đã kết thúc.";

        if (!Enum.IsDefined(request.DiscountType))
            return "Kiểu giảm giá không hợp lệ.";
        if (request.DiscountType == DiscountType.FixedPrice)
        {
            if (!VndPriceRules.IsValidPrice(request.Value))
            {
                return "Giá giảm cố định phải là số nguyên dương theo đơn vị VNĐ.";
            }

            if (request.Value >= basePrice)
            {
                return "Giá giảm cố định phải nhỏ hơn giá gốc.";
            }
        }
        if (request.DiscountType == DiscountType.Percentage &&
            (request.Value < 1 || request.Value > 99))
        {
            return "Phần trăm giảm phải từ 1 đến 99.";
        }

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
        if (!VndPriceRules.IsValidPrice(newPrice))
        {
            return "Giá gốc phải là số nguyên dương theo đơn vị VNĐ và không vượt quá 99.999.999đ.";
        }
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

    private static PriceManagementRow BuildRow(Product product, ProductVariant? variant, decimal basePrice,
        int priceRevision, int stock, IEnumerable<PriceSchedule> schedules, DateTimeOffset now)
    {
        var list = schedules.Where(s => !s.IsCancelled).ToList();
        var quote = PriceCalculator.CalculateQuote(basePrice, list, now);
        var currentSchedule = quote.ScheduleId.HasValue
            ? list.First(schedule => schedule.Id == quote.ScheduleId.Value)
            : null;

        return new PriceManagementRow
        {
            ProductId = product.Id,
            ProductVariantId = variant?.Id,
            ProductName = product.Name,
            VariantName = variant?.Name,
            SKU = variant?.SKU,
            BasePrice = basePrice,
            EffectivePrice = quote.EffectivePrice,
            StockQuantity = stock,
            PriceRevision = priceRevision,
            CurrentSchedule = currentSchedule,
            UpcomingSchedule = list
                .Where(schedule => schedule.StartsAt > now)
                .OrderBy(schedule => schedule.StartsAt)
                .ThenBy(schedule => schedule.Id)
                .FirstOrDefault(),
            Schedules = schedules.OrderBy(schedule => schedule.StartsAt).ThenBy(schedule => schedule.Id).ToList()
        };
    }
}
