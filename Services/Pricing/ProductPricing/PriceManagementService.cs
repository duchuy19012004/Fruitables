using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Data;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Chat.Knowledge;

namespace Fruitables.Services.Pricing.ProductPricing;

public sealed class PriceManagementService : IPriceManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IRealtimeNotifier? _notifier;
    private readonly IIndexingService? _indexing;
    private readonly ILogger<PriceManagementService>? _logger;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter? _auditLogWriter;

    private bool UseTargetSchema => _dbContext?.Database.IsSqlServer() == true;

    public PriceManagementService(IUnitOfWork unitOfWork, TimeProvider timeProvider,
        IRealtimeNotifier? notifier = null, IIndexingService? indexing = null,
        ILogger<PriceManagementService>? logger = null,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _indexing = indexing;
        _logger = logger;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? (dbContext == null ? null : new AuditLogWriter(dbContext));
    }

    public async Task<PriceManagementViewModel> GetDashboardAsync(PriceDashboardQuery q)
    {
        var now = _timeProvider.GetUtcNow();
        IQueryable<Product> query = _unitOfWork.Products.Query()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Variants);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.Name.Contains(q.Search) || p.Variants.Any(v => v.SKU.Contains(q.Search)));

        var products = await query.ToListAsync();
        var allProducts = await _unitOfWork.Products.Query()
            .Where(product => !product.IsDeleted)
            .Include(product => product.Variants)
            .ToListAsync();
        var schedules = await GetSchedulesAsync();
        var rows = new List<PriceManagementRow>();
        foreach (var product in products)
        {
            var activeVariants = product.Variants.Where(v => v.IsActive).OrderBy(v => v.Name).ToList();
            if (activeVariants.Count == 0)
            {
                rows.Add(BuildRow(product, null, product.Price, product.PriceRevision,
                    product.StockQuantity,
                    schedules.Where(schedule => schedule.ProductId == product.Id && schedule.ProductVariantId == null),
                    now));
                continue;
            }

            foreach (var variant in activeVariants)
                rows.Add(BuildRow(product, variant, variant.Price, variant.PriceRevision,
                    variant.StockQuantity,
                    schedules.Where(schedule => schedule.ProductId == product.Id && schedule.ProductVariantId == variant.Id),
                    now));
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
        var targetProducts = allProducts.OrderBy(p => p.Name).ToList();
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

        // Tab Lịch giảm giá: JSON payloads are materialized once and filtered in memory.
        var productById = allProducts.ToDictionary(product => product.Id);
        var scheduleRows = schedules
            .Select(schedule => new
            {
                Schedule = schedule,
                Product = productById.GetValueOrDefault(schedule.ProductId),
                Variant = productById.GetValueOrDefault(schedule.ProductId)?.Variants
                    .FirstOrDefault(variant => variant.Id == schedule.ProductVariantId)
            })
            .Where(row => row.Product != null)
            .ToList();
        foreach (var row in scheduleRows)
        {
            row.Schedule.Product = row.Product!;
            row.Schedule.ProductVariant = row.Variant;
        }
        if (!string.IsNullOrWhiteSpace(q.ScheduleSearch))
        {
            scheduleRows = scheduleRows.Where(row =>
                row.Product!.Name.Contains(q.ScheduleSearch, StringComparison.OrdinalIgnoreCase) ||
                (row.Variant != null &&
                    (row.Variant.Name.Contains(q.ScheduleSearch, StringComparison.OrdinalIgnoreCase) ||
                     row.Variant.SKU.Contains(q.ScheduleSearch, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        var statusCounts = new Dictionary<string, int>
        {
            ["all"] = scheduleRows.Count,
            ["active"] = 0,
            ["scheduled"] = 0,
            ["ended"] = 0,
            ["cancelled"] = 0,
            ["stopped"] = 0
        };
        foreach (var row in scheduleRows)
        {
            var key = row.Schedule.GetStatus(now) switch
            {
                PriceScheduleStatus.Scheduled => "scheduled",
                PriceScheduleStatus.Ended => "ended",
                PriceScheduleStatus.Cancelled => "cancelled",
                PriceScheduleStatus.StoppedEarly => "stopped",
                _ => "active"
            };
            statusCounts[key]++;
        }

        var filteredScheduleRows = q.ScheduleStatus switch
        {
            "active" => scheduleRows.Where(row => row.Schedule.GetStatus(now) == PriceScheduleStatus.Active).ToList(),
            "scheduled" => scheduleRows.Where(row => row.Schedule.GetStatus(now) == PriceScheduleStatus.Scheduled).ToList(),
            "ended" => scheduleRows.Where(row => row.Schedule.GetStatus(now) == PriceScheduleStatus.Ended).ToList(),
            "stopped" => scheduleRows.Where(row => row.Schedule.GetStatus(now) == PriceScheduleStatus.StoppedEarly).ToList(),
            "cancelled" => scheduleRows.Where(row => row.Schedule.GetStatus(now) == PriceScheduleStatus.Cancelled).ToList(),
            _ => scheduleRows
        };
        var schTotal = filteredScheduleRows.Count;
        var schPage = Math.Max(1, q.SchedulePage);
        var schItems = filteredScheduleRows
            .OrderByDescending(row => row.Schedule.StartsAt)
            .Skip((schPage - 1) * q.SchedulePageSize)
            .Take(q.SchedulePageSize)
            .Select(row => row.Schedule)
            .ToList();

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
            if (_dbContext == null)
                return PriceManagementResult.Fail("Dịch vụ lịch giá chưa được cấu hình.");

            var validation = await ValidateScheduleAsync(request, null);
            if (validation != null) return PriceManagementResult.Fail(validation);

            var now = _timeProvider.GetUtcNow();
            PriceSchedule? legacySchedule = null;
            if (!UseTargetSchema)
            {
                legacySchedule = new PriceSchedule
                {
                    ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId,
                    DiscountType = request.DiscountType,
                    Value = request.Value,
                    StartsAt = request.StartsAt.ToUniversalTime(),
                    EndsAt = request.EndsAt?.ToUniversalTime(),
                    CreatedByAdminId = adminId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.PriceSchedules.AddAsync(legacySchedule);
                await _unitOfWork.SaveChangesAsync();
            }

            var payload = new PriceSchedulePayload
            {
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                LegacyScheduleId = legacySchedule?.Id ?? 0,
                DiscountType = request.DiscountType,
                Value = request.Value,
                StartsAt = request.StartsAt.ToUniversalTime(),
                EndsAt = request.EndsAt?.ToUniversalTime(),
                CreatedByAdminId = adminId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var promotion = new Promotion
            {
                Type = "price-schedule",
                Code = legacySchedule is null
                    ? $"price-schedule:new-{Guid.NewGuid():N}"
                    : $"price-schedule:{legacySchedule.Id}",
                PayloadJson = _serializer.Serialize(payload),
                IsActive = true,
                StartsAt = payload.StartsAt,
                EndsAt = payload.EndsAt,
                Revision = payload.Revision,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime
            };
            _dbContext.Promotions.Add(promotion);
            await _dbContext.SaveChangesAsync();
            await AddLogAsync(request.ProductId, adminId, "PriceScheduleCreate", $"Tạo lịch giảm giá từ {payload.StartsAt:O}");
            return PriceManagementResult.Ok(promotion.Revision);
        });
        if (result.Success) await PublishPriceChangeAsync(request.ProductId, request.ProductVariantId);
        return result;
    }

    public async Task<PriceManagementResult> UpdateScheduleAsync(int id, SavePriceScheduleRequest request, int adminId)
    {
        var result = await RunSerializedWriteAsync(async () =>
        {
            if (_dbContext == null)
                return PriceManagementResult.Fail("Dịch vụ lịch giá chưa được cấu hình.");

            var found = await GetScheduleAsync(id);
            if (found == null) return PriceManagementResult.Fail("Không tìm thấy lịch giá.");
            var (promotion, schedule) = found.Value;
            if (request.ExpectedRevision <= 0 || schedule.Revision != request.ExpectedRevision)
                return PriceManagementResult.Fail("Lịch giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");
            if (schedule.GetStatus(_timeProvider.GetUtcNow()) != PriceScheduleStatus.Scheduled)
                return PriceManagementResult.Fail("Chỉ lịch chưa bắt đầu mới được chỉnh sửa.");
            if (schedule.ProductId != request.ProductId || schedule.ProductVariantId != request.ProductVariantId)
                return PriceManagementResult.Fail("Không thể đổi đối tượng của một lịch giá đã tạo.");

            var validation = await ValidateScheduleAsync(request, id);
            if (validation != null) return PriceManagementResult.Fail(validation);
            var legacySchedule = UseTargetSchema
                ? schedule
                : await EnsureLegacyScheduleAsync(promotion, schedule);
            schedule.Id = legacySchedule.Id;
            schedule.DiscountType = request.DiscountType;
            schedule.Value = request.Value;
            schedule.StartsAt = request.StartsAt.ToUniversalTime();
            schedule.EndsAt = request.EndsAt?.ToUniversalTime();
            schedule.UpdatedAt = _timeProvider.GetUtcNow();
            schedule.Revision++;
            promotion.PayloadJson = _serializer.Serialize(ToPayload(schedule));
            promotion.StartsAt = schedule.StartsAt;
            promotion.EndsAt = schedule.EndsAt;
            promotion.Revision = schedule.Revision;
            promotion.UpdatedAt = schedule.UpdatedAt.UtcDateTime;
            if (!UseTargetSchema)
                CopyToLegacySchedule(legacySchedule, schedule);
            await AddLogAsync(request.ProductId, adminId, "PriceScheduleUpdate", $"Cập nhật lịch giá #{id}");
            await _dbContext.SaveChangesAsync();
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
            if (_dbContext == null)
                return PriceManagementResult.Fail("Dịch vụ lịch giá chưa được cấu hình.");

            var found = await GetScheduleAsync(id);
            if (found == null)
                return PriceManagementResult.Fail("Không tìm thấy lịch giá.");
            var (promotion, schedule) = found.Value;

            if (request.ExpectedRevision <= 0 || schedule.Revision != request.ExpectedRevision)
                return PriceManagementResult.Fail("Lịch giá đã thay đổi bởi người khác. Vui lòng tải lại trang.");

            var now = _timeProvider.GetUtcNow();
            var status = schedule.GetStatus(now);
            if (status is PriceScheduleStatus.Ended or PriceScheduleStatus.Cancelled or PriceScheduleStatus.StoppedEarly)
                return PriceManagementResult.Fail("Lịch đã kết thúc hoặc đã dừng.");

            var reason = request.Reason?.Trim();
            if (reason?.Length > 500)
                return PriceManagementResult.Fail("Lý do hủy không được vượt quá 500 ký tự.");

            var legacySchedule = UseTargetSchema
                ? schedule
                : await EnsureLegacyScheduleAsync(promotion, schedule);
            schedule.Id = legacySchedule.Id;
            schedule.IsCancelled = true;
            schedule.CancelledAt = now;
            schedule.CancelledByAdminId = adminId;
            schedule.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
            schedule.UpdatedAt = now;
            schedule.Revision++;
            promotion.PayloadJson = _serializer.Serialize(ToPayload(schedule));
            promotion.IsActive = false;
            promotion.UpdatedAt = now.UtcDateTime;
            promotion.Revision = schedule.Revision;

            productId = schedule.ProductId;
            variantId = schedule.ProductVariantId;

            var action = status == PriceScheduleStatus.Active
                ? "PriceScheduleStoppedEarly"
                : "PriceScheduleCancel";
            var detail = $"{action} #{id}; reason={schedule.CancellationReason ?? "không có"}; cancelledAt={now:O}";
            if (!UseTargetSchema)
                CopyToLegacySchedule(legacySchedule, schedule);
            await AddLogAsync(schedule.ProductId, adminId, action, detail);
            await _dbContext.SaveChangesAsync();

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
        var overlaps = (await GetSchedulesAsync())
            .Where(schedule => schedule.Id != excludingId)
            .Any(schedule => schedule.ProductId == request.ProductId &&
                schedule.ProductVariantId == request.ProductVariantId &&
                !schedule.IsCancelled &&
                (!schedule.EndsAt.HasValue || start < schedule.EndsAt.Value) &&
                (!end.HasValue || schedule.StartsAt < end.Value));
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
        var invalidFixed = (await GetSchedulesAsync()).Any(schedule =>
            schedule.ProductId == target.ProductId &&
            schedule.ProductVariantId == target.ProductVariantId &&
            !schedule.IsCancelled &&
            (!schedule.EndsAt.HasValue || schedule.EndsAt > now) &&
            schedule.DiscountType == DiscountType.FixedPrice &&
            schedule.Value >= newPrice);
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

    private Task AddLogAsync(int productId, int adminId, string action, string details) =>
        _auditLogWriter?.WriteAsync(
            action,
            "Product",
            productId,
            adminId,
            newValue: System.Text.Json.JsonSerializer.Serialize(new { details })) ?? Task.CompletedTask;

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
        int priceRevision, decimal stock, IEnumerable<PriceSchedule> schedules, DateTimeOffset now)
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

    private async Task<List<PriceSchedule>> GetSchedulesAsync()
    {
        if (_dbContext == null)
            return [];

        var promotions = await _dbContext.Promotions.AsNoTracking()
            .Where(promotion => promotion.Type == "price-schedule")
            .ToListAsync();
        return promotions.Select(ToSchedule).ToList();
    }

    private async Task<(Promotion Promotion, PriceSchedule Schedule)?> GetScheduleAsync(int id)
    {
        if (_dbContext == null)
            return null;

        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(item => item.Type == "price-schedule" &&
                (item.Id == id || item.Code == $"price-schedule:{id}"));
        if (promotion == null)
            return null;
        return (promotion, ToSchedule(promotion));
    }

    private PriceSchedule ToSchedule(Promotion promotion)
    {
        var payload = _serializer.Deserialize<PriceSchedulePayload>(promotion.PayloadJson);
        return new PriceSchedule
        {
            Id = payload.LegacyScheduleId ?? ParseLegacyScheduleId(promotion.Code) ?? promotion.Id,
            ProductId = payload.ProductId,
            ProductVariantId = payload.ProductVariantId,
            DiscountType = payload.DiscountType,
            Value = payload.Value,
            StartsAt = payload.StartsAt,
            EndsAt = payload.EndsAt,
            IsCancelled = payload.IsCancelled,
            CancelledAt = payload.CancelledAt,
            CancelledByAdminId = payload.CancelledByAdminId,
            CancellationReason = payload.CancellationReason,
            Revision = payload.Revision,
            CreatedByAdminId = payload.CreatedByAdminId,
            CreatedAt = payload.CreatedAt,
            UpdatedAt = payload.UpdatedAt
        };
    }

    private static PriceSchedulePayload ToPayload(PriceSchedule schedule) => new()
    {
        ProductId = schedule.ProductId,
        ProductVariantId = schedule.ProductVariantId,
        LegacyScheduleId = schedule.Id,
        DiscountType = schedule.DiscountType,
        Value = schedule.Value,
        StartsAt = schedule.StartsAt,
        EndsAt = schedule.EndsAt,
        IsCancelled = schedule.IsCancelled,
        CancelledAt = schedule.CancelledAt,
        CancelledByAdminId = schedule.CancelledByAdminId,
        CancellationReason = schedule.CancellationReason,
        Revision = schedule.Revision,
        CreatedByAdminId = schedule.CreatedByAdminId,
        CreatedAt = schedule.CreatedAt,
        UpdatedAt = schedule.UpdatedAt
    };

    private async Task<PriceSchedule> EnsureLegacyScheduleAsync(Promotion promotion, PriceSchedule schedule)
    {
        if (_dbContext == null)
            return schedule;

        var legacy = await _dbContext.PriceSchedules
            .FirstOrDefaultAsync(item => item.Id == schedule.Id);
        if (legacy != null)
            return legacy;

        legacy = new PriceSchedule
        {
            ProductId = schedule.ProductId,
            ProductVariantId = schedule.ProductVariantId,
            DiscountType = schedule.DiscountType,
            Value = schedule.Value,
            StartsAt = schedule.StartsAt,
            EndsAt = schedule.EndsAt,
            IsCancelled = schedule.IsCancelled,
            CancelledAt = schedule.CancelledAt,
            CancelledByAdminId = schedule.CancelledByAdminId,
            CancellationReason = schedule.CancellationReason,
            Revision = schedule.Revision,
            CreatedByAdminId = schedule.CreatedByAdminId,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
        await _dbContext.PriceSchedules.AddAsync(legacy);
        await _dbContext.SaveChangesAsync();
        promotion.Code = $"price-schedule:{legacy.Id}";
        return legacy;
    }

    private static void CopyToLegacySchedule(PriceSchedule target, PriceSchedule source)
    {
        target.ProductId = source.ProductId;
        target.ProductVariantId = source.ProductVariantId;
        target.DiscountType = source.DiscountType;
        target.Value = source.Value;
        target.StartsAt = source.StartsAt;
        target.EndsAt = source.EndsAt;
        target.IsCancelled = source.IsCancelled;
        target.CancelledAt = source.CancelledAt;
        target.CancelledByAdminId = source.CancelledByAdminId;
        target.CancellationReason = source.CancellationReason;
        target.Revision = source.Revision;
        target.CreatedByAdminId = source.CreatedByAdminId;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }

    private static int? ParseLegacyScheduleId(string? code)
    {
        const string prefix = "price-schedule:";
        return code != null && code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[prefix.Length..], out var id) && id > 0
            ? id
            : null;
    }
}
