using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Models.Returns;
using Fruitables.Repositories.Interfaces;
using Fruitables.Services.Communications;
using Fruitables.Services.Infrastructure.Auditing;
using Fruitables.Services.Infrastructure.Json;
using Fruitables.Services.Pricing.Combos;
using Fruitables.Services.Pricing.ProductPricing;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Orders.Cart;
using Fruitables.Services.Orders;

namespace Fruitables.Services.Catalog.Combos;

public class ComboService : IComboService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductPricingService _pricing;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ComboService>? _logger;
    private readonly ApplicationDbContext? _dbContext;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly IAuditLogWriter? _auditLogWriter;

    public ComboService(
        IUnitOfWork unitOfWork,
        IProductPricingService pricing,
        TimeProvider? timeProvider = null,
        ILogger<ComboService>? logger = null,
        ApplicationDbContext? dbContext = null,
        IJsonDocumentSerializer? serializer = null,
        IAuditLogWriter? auditLogWriter = null)
    {
        _unitOfWork = unitOfWork;
        _pricing = pricing;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
        _dbContext = dbContext;
        _serializer = serializer ?? new VersionedJsonSerializer();
        _auditLogWriter = auditLogWriter ?? (dbContext == null ? null : new AuditLogWriter(dbContext));
    }

    public async Task<IReadOnlyList<ComboListRowViewModel>> GetAdminListAsync()
    {
        var combos = await LoadCombosAsync();

        var quotes = await GetQuotesForCombosAsync(combos);
        var now = _timeProvider.GetUtcNow();
        var result = new List<ComboListRowViewModel>();
        foreach (var combo in combos)
        {
            var card = BuildCard(combo, quotes);
            result.Add(new ComboListRowViewModel
            {
                Id = combo.Id,
                Name = combo.Name,
                ImageUrl = combo.ImageUrl,
                ItemCount = combo.Items.Count,
                TotalPrice = card?.TotalPrice ?? 0,
                OriginalPrice = card?.OriginalPrice ?? 0,
                Savings = card?.Savings ?? 0,
                PricingType = combo.PricingType,
                IsActive = combo.IsAvailableAt(now),
                IsSellable = combo.IsAvailableAt(now) && combo.Items.Count >= 2 && card?.Items.All(item => item.IsAvailable) == true,
                Status = combo.GetEffectiveStatus(now),
                StartsAt = combo.StartsAt,
                EndsAt = combo.EndsAt
            });
        }
        return result;
    }

    public async Task<ComboFormViewModel?> GetForEditAsync(int id)
    {
        var combo = (await LoadCombosAsync([id])).FirstOrDefault();

        if (combo == null) return null;

        var products = await GetProductOptionsAsync();

        return new ComboFormViewModel
        {
            Id = combo.Id,
            Revision = combo.Revision,
            Name = combo.Name,
            Slug = combo.Slug,
            Description = combo.Description,
            ImageUrl = combo.ImageUrl,
            IsActive = combo.IsActive,
            Status = combo.Status,
            StartsAt = combo.StartsAt,
            EndsAt = combo.EndsAt,
            PricingType = combo.PricingType,
            FixedPrice = combo.FixedPrice,
            DiscountValue = combo.DiscountValue,
            AllowCouponStacking = combo.AllowCouponStacking,
            SortOrder = combo.SortOrder,
            Items = combo.Items.OrderBy(i => i.SortOrder).Select(i => new ComboItemFormModel
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity,
                SortOrder = i.SortOrder
            }).ToList(),
            Products = products.ToList()
        };
    }

    public async Task<ComboResult> CreateAsync(ComboFormViewModel model, int? adminId = null)
    {
        var validationError = await ValidateComboAsync(model);
        if (validationError != null)
            return ComboResult.Fail(validationError);

        var slug = await ResolveSlugAsync(model.Slug, model.Name);
        if (slug == null)
            return ComboResult.Fail("Slug đã tồn tại hoặc không hợp lệ.");

        if (_dbContext == null)
            return ComboResult.Fail("Dịch vụ combo chưa được cấu hình.");

        return await RunAtomicWriteAsync(async () =>
        {
            var payload = new ComboPayload
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
                IsActive = model.Status != ComboLifecycleStatus.Archived,
                Status = model.Status,
                StartsAt = model.StartsAt,
                EndsAt = model.EndsAt,
                PricingType = model.PricingType,
                FixedPrice = model.PricingType == ComboPricingType.FixedPrice ? model.FixedPrice : null,
                DiscountValue = model.PricingType is ComboPricingType.PercentageDiscount or ComboPricingType.FixedDiscount ? model.DiscountValue : null,
                AllowCouponStacking = model.AllowCouponStacking,
                SortOrder = model.SortOrder,
                Items = BuildPayloadItems(model.Items)
            };

            var promotion = new Promotion
            {
                Type = "combo",
                Code = $"combo:new-{Guid.NewGuid():N}",
                PayloadJson = _serializer.Serialize(payload),
                IsActive = payload.IsActive,
                StartsAt = payload.StartsAt,
                EndsAt = payload.EndsAt,
                Revision = payload.Revision,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Promotions.Add(promotion);
            await _dbContext.SaveChangesAsync();
            await SyncLegacyComboAsync(promotion, payload);

            var combo = ToCombo(promotion, payload, new Dictionary<int, Product>());
            await AddAuditAsync(combo, adminId, ComboAuditActions.Create, Describe(combo));
            _logger?.LogInformation("Combo {ComboId} created at revision {Revision} by admin {AdminId}", combo.Id, combo.Revision, adminId);
            return ComboResult.Ok(combo);
        });
    }

    public async Task<ComboResult> UpdateAsync(int id, ComboFormViewModel model, int? adminId = null)
    {
        if (_dbContext == null)
            return ComboResult.Fail("Dịch vụ combo chưa được cấu hình.");

        return await RunAtomicWriteAsync(async () =>
        {
            var promotion = await _dbContext.Promotions
                .FirstOrDefaultAsync(item => item.Id == id && item.Type == "combo");
            if (promotion == null)
                return ComboResult.Fail("Không tìm thấy combo.");
            var currentPayload = _serializer.Deserialize<ComboPayload>(promotion.PayloadJson);
            if (model.Revision != currentPayload.Revision || model.Revision != promotion.Revision)
                return ComboResult.Fail("Combo đã được người khác cập nhật. Vui lòng tải lại trang.");

            var validationError = await ValidateComboAsync(model);
            if (validationError != null)
                return ComboResult.Fail(validationError);

            var slug = await ResolveSlugAsync(model.Slug, model.Name, id);
            if (slug == null)
                return ComboResult.Fail("Slug đã tồn tại hoặc không hợp lệ.");

            var updatedPayload = new ComboPayload
            {
                Name = model.Name.Trim(),
                Slug = slug,
                Description = model.Description?.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
                IsActive = model.Status != ComboLifecycleStatus.Archived,
                Status = model.Status,
                StartsAt = model.StartsAt,
                EndsAt = model.EndsAt,
                PricingType = model.PricingType,
                FixedPrice = model.PricingType == ComboPricingType.FixedPrice ? model.FixedPrice : null,
                DiscountValue = model.PricingType is ComboPricingType.PercentageDiscount or ComboPricingType.FixedDiscount ? model.DiscountValue : null,
                AllowCouponStacking = model.AllowCouponStacking,
                Revision = promotion.Revision + 1,
                SortOrder = model.SortOrder,
                Items = BuildPayloadItems(model.Items)
            };
            promotion.PayloadJson = _serializer.Serialize(updatedPayload);
            promotion.IsActive = updatedPayload.IsActive;
            promotion.StartsAt = updatedPayload.StartsAt;
            promotion.EndsAt = updatedPayload.EndsAt;
            promotion.Revision = updatedPayload.Revision;
            promotion.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncLegacyComboAsync(promotion, updatedPayload);
            var updated = ToCombo(promotion, updatedPayload, new Dictionary<int, Product>());
            await AddAuditAsync(updated, adminId, ComboAuditActions.Update, Describe(updated));
            _logger?.LogInformation("Combo {ComboId} updated to revision {Revision} by admin {AdminId}", updated.Id, updated.Revision, adminId);
            return ComboResult.Ok(updated);
        }
        );
    }

    public async Task<ComboResult> DeleteAsync(int id, int? adminId = null)
    {
        if (_dbContext == null)
            return ComboResult.Fail("Dịch vụ combo chưa được cấu hình.");

        return await RunAtomicWriteAsync(async () =>
        {
            var promotion = await _dbContext.Promotions
                .FirstOrDefaultAsync(item => item.Id == id && item.Type == "combo");
            if (promotion == null)
                return ComboResult.Fail("Không tìm thấy combo.");

            var payload = _serializer.Deserialize<ComboPayload>(promotion.PayloadJson);
            var archived = new ComboPayload
            {
                Name = payload.Name,
                Slug = payload.Slug,
                Description = payload.Description,
                ImageUrl = payload.ImageUrl,
                IsActive = false,
                Status = ComboLifecycleStatus.Archived,
                StartsAt = payload.StartsAt,
                EndsAt = payload.EndsAt,
                PricingType = payload.PricingType,
                FixedPrice = payload.FixedPrice,
                DiscountValue = payload.DiscountValue,
                AllowCouponStacking = payload.AllowCouponStacking,
                Revision = promotion.Revision + 1,
                SortOrder = payload.SortOrder,
                Items = payload.Items
            };
            promotion.PayloadJson = _serializer.Serialize(archived);
            promotion.IsActive = false;
            promotion.Revision = archived.Revision;
            promotion.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await SyncLegacyComboAsync(promotion, archived);
            var combo = ToCombo(promotion, archived, new Dictionary<int, Product>());
            await AddAuditAsync(combo, adminId, ComboAuditActions.Archive, "Lưu trữ combo.");
            _logger?.LogInformation("Combo {ComboId} archived by admin {AdminId}", combo.Id, adminId);
            return ComboResult.Ok(combo);
        }
        );
    }

    public async Task<IReadOnlyList<ComboProductOptionViewModel>> GetProductOptionsAsync()
    {
        var products = await _unitOfWork.Products.Query()
            .Where(p => p.IsActive && !p.IsDeleted)
            .Include(p => p.Variants)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(p => new ComboProductOptionViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Unit = p.Unit,
            StockQuantity = p.StockQuantity,
            Variants = p.Variants
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .Select(v => new ComboVariantOptionViewModel
                {
                    Id = v.Id,
                    Name = v.Name,
                    SKU = v.SKU,
                    StockQuantity = v.StockQuantity
                })
                .ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<ComboCardViewModel>> GetActiveComboCardsAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var combos = (await LoadCombosAsync())
            .Where(combo => combo.IsAvailableAt(now))
            .OrderBy(combo => combo.SortOrder)
            .ThenByDescending(combo => combo.CreatedAt)
            .ToList();

        var quotes = await GetQuotesForCombosAsync(combos);
        var cards = new List<ComboCardViewModel>();
        foreach (var combo in combos)
        {
            var card = BuildCard(combo, quotes);
            if (card != null && card.Items.Count >= 2 && card.Items.All(i => i.IsAvailable))
                cards.Add(card);
        }
        return cards;
    }

    public async Task<AddComboToCartResult> AddComboToCartAsync(string sessionId, int comboId, ICartService cartService)
    {
        var combo = (await LoadCombosAsync([comboId])).FirstOrDefault();

        if (combo == null || !combo.IsAvailableAt(_timeProvider.GetUtcNow()))
            return new AddComboToCartResult { Success = false, Message = "Combo chưa đến lịch bán hoặc đã ngừng bán." };

        var targets = combo.Items
            .Select(i => new PriceTargetKey(i.ProductId, i.ProductVariantId))
            .Distinct()
            .ToList();

        var quotes = targets.Any()
            ? await _pricing.GetQuotesAsync(targets)
            : new Dictionary<PriceTargetKey, PriceQuote>();

        if (combo.Items.Count < 2)
            return new AddComboToCartResult { Success = false, Message = "Combo chưa có đủ sản phẩm." };

            var unavailable = combo.Items
                .Select(item => new { Item = item, Reason = GetUnavailableReason(item, quotes) })
                .Where(item => !string.IsNullOrEmpty(item.Reason))
                .Select(item => $"{item.Item.Product?.Name ?? $"Sản phẩm #{item.Item.ProductId}"} ({item.Reason})")
                .ToList();
        if (unavailable.Count > 0)
        {
            return new AddComboToCartResult
            {
                Success = false,
                SkippedItems = unavailable,
                Message = "Không thể thêm combo vì: " + string.Join(", ", unavailable) + "."
            };
        }

        var cartComboId = await ResolveLegacyComboIdAsync(combo.Id);
        var cartResult = await cartService.AddComboToCartAsync(sessionId, cartComboId);

        return new AddComboToCartResult
        {
            Success = cartResult.Success,
            AddedCount = cartResult.Success ? combo.Items.Count : 0,
            Message = cartResult.Success
                ? $"Đã thêm toàn bộ combo '{combo.Name}' vào giỏ hàng."
                : cartResult.Message
        };
    }

    private ComboCardViewModel? BuildCard(
        Combo combo,
        IReadOnlyDictionary<PriceTargetKey, PriceQuote> quotes)
    {
        if (combo.Items == null) return null;

        var items = combo.Items
            .OrderBy(i => i.SortOrder)
            .Select(i =>
            {
                var key = new PriceTargetKey(i.ProductId, i.ProductVariantId);
                var reason = GetUnavailableReason(i, quotes);
                var available = string.IsNullOrEmpty(reason);
                var quote = available && quotes.TryGetValue(key, out var q) ? q : null;

                return new ComboCardItemViewModel
                {
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductName = i.Product?.Name ?? "[Đã xóa]",
                    ProductImage = i.Product?.Images?.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? i.Product?.Images?.FirstOrDefault()?.ImageUrl,
                    VariantName = i.ProductVariant?.Name,
                    Quantity = i.Quantity,
                    UnitPrice = quote?.EffectivePrice ?? 0,
                    IsAvailable = available,
                    UnavailableReason = reason
                };
            })
            .ToList();

        var originalPrice = items.Where(item => item.IsAvailable).Sum(item => item.UnitPrice * item.Quantity);
        var price = ComboPricingCalculator.Calculate(
            combo.PricingType,
            originalPrice,
            combo.FixedPrice,
            combo.DiscountValue);

        return new ComboCardViewModel
        {
            Id = combo.Id,
            Name = combo.Name,
            Slug = combo.Slug,
            Description = combo.Description,
            ImageUrl = combo.ImageUrl,
            OriginalPrice = price.OriginalTotal,
            TotalPrice = price.FinalTotal,
            Savings = price.Discount,
            AllowCouponStacking = combo.AllowCouponStacking,
            Revision = combo.Revision,
            Items = items
        };
    }

    private async Task<IReadOnlyDictionary<PriceTargetKey, PriceQuote>> GetQuotesForCombosAsync(
        IEnumerable<Combo> combos)
    {
        var targets = combos
            .SelectMany(combo => combo.Items)
            .Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .Distinct()
            .ToList();

        return targets.Count == 0
            ? new Dictionary<PriceTargetKey, PriceQuote>()
            : await _pricing.GetQuotesAsync(targets);
    }

    public async Task<ComboAuditViewModel?> GetAuditAsync(int comboId, int take = 100)
    {
        var combo = (await LoadCombosAsync([comboId])).FirstOrDefault();
        if (combo == null) return null;

        if (_dbContext == null)
            return new ComboAuditViewModel { ComboId = combo.Id, ComboName = combo.Name };

        var logs = await _dbContext.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == "Combo" && log.EntityId == comboId)
            .OrderByDescending(log => log.ChangedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync();
        var adminIds = logs.Select(log => log.ChangedByAdminId).Where(id => id > 0).Distinct().ToList();
        var admins = await _dbContext.Users.AsNoTracking()
            .Where(user => adminIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name);
        var items = logs.Select(log => new ComboAuditRowViewModel
            {
                Id = log.Id,
                Action = log.Action,
                Revision = ReadRevision(log.NewValue),
                Details = ReadDetails(log.NewValue),
                AdminName = admins.GetValueOrDefault(log.ChangedByAdminId) ?? "Hệ thống",
                CreatedAt = log.ChangedAt
            })
            .ToList();

        return new ComboAuditViewModel { ComboId = combo.Id, ComboName = combo.Name, Items = items };
    }

    public async Task<ComboReportViewModel> GetReportAsync(DateTime from, DateTime to)
    {
        var normalizedFrom = from.Date;
        var normalizedTo = to.Date;
        if (normalizedTo < normalizedFrom) (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        if ((normalizedTo - normalizedFrom).TotalDays > 366)
            normalizedFrom = normalizedTo.AddDays(-366);
        var exclusiveTo = normalizedTo.AddDays(1);

        var orders = await _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Where(order => order.CreatedAt >= normalizedFrom && order.CreatedAt < exclusiveTo &&
                order.Items.Any(item => item.SourceComboId.HasValue))
            .Include(order => order.Items)
            .Include(order => order.ReturnRequest)
                .ThenInclude(request => request!.Items)
            .Include(order => order.ReturnRequest)
                .ThenInclude(request => request!.Refund)
            .ToListAsync();

        var orderGroups = orders.SelectMany(order => order.Items
            .Where(item => item.SourceComboId.HasValue)
            .GroupBy(item => new { ComboId = item.SourceComboId!.Value, item.ComboNameSnapshot, item.ComboRevision })
            .Select(group => new
            {
                order.Id,
                order.Status,
                order.PaymentStatus,
                group.Key.ComboId,
                Name = group.Key.ComboNameSnapshot ?? $"Combo #{group.Key.ComboId}",
                Quantity = group.Max(item => item.ComboQuantity ?? 1),
                Discount = group.Sum(item => item.ComboDiscount),
                Revenue = group.Sum(item => item.Total),
                RefundedRevenue = order.PaymentStatus == PaymentStatus.Refunded
                    ? group.Sum(item => item.Total)
                    : order.ReturnRequest?.Refund?.Status == RefundStatus.Succeeded
                        ? order.ReturnRequest.Items
                            .Where(returnItem => group.Any(item => item.Id == returnItem.OrderItemId))
                            .Sum(returnItem => returnItem.ApprovedAmount)
                        : 0m
            })).ToList();

        var rows = orderGroups
            .GroupBy(item => new { item.ComboId, item.Name })
            .Select(group => new ComboReportRowViewModel
            {
                ComboId = group.Key.ComboId,
                ComboName = group.Key.Name,
                BundlesSold = group.Where(item => item.Status == OrderStatus.Delivered).Sum(item => item.Quantity),
                OrderCount = group.Select(item => item.Id).Distinct().Count(),
                ComboDiscount = group.Sum(item => item.Discount),
                DeliveredRevenue = group.Where(item => item.Status == OrderStatus.Delivered).Sum(item => item.Revenue),
                RefundedRevenue = group.Sum(item => item.RefundedRevenue)
            })
            .OrderByDescending(row => row.NetRevenue)
            .ThenBy(row => row.ComboName)
            .ToList();

        return new ComboReportViewModel { From = normalizedFrom, To = normalizedTo, Rows = rows };
    }

    private static string Describe(Combo combo) =>
        $"Status={combo.Status}; StartsAt={combo.StartsAt:O}; EndsAt={combo.EndsAt:O}; " +
        $"PricingType={combo.PricingType}; FixedPrice={combo.FixedPrice}; DiscountValue={combo.DiscountValue}; " +
        $"CouponStacking={combo.AllowCouponStacking}; Items={combo.Items.Count}; Revision={combo.Revision}";

    private async Task<List<Combo>> LoadCombosAsync(IReadOnlyCollection<int>? ids = null)
    {
        if (_dbContext == null)
            return [];

        var query = _dbContext.Promotions.AsNoTracking()
            .Where(item => item.Type == "combo");
        if (ids is { Count: > 0 })
            query = query.Where(item => ids.Contains(item.Id));

        var promotions = await query.ToListAsync();
        var payloads = promotions.Select(promotion =>
            (Promotion: promotion, Payload: _serializer.Deserialize<ComboPayload>(promotion.PayloadJson)))
            .ToList();
        var productIds = payloads.SelectMany(item => item.Payload.Items.Select(line => line.ProductId))
            .Distinct()
            .ToArray();
        var products = productIds.Length == 0
            ? new Dictionary<int, Product>()
            : (await _unitOfWork.Products.Query()
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .Include(product => product.Variants)
                .ToListAsync())
                .ToDictionary(product => product.Id);
        Fruitables.Services.Catalog.Products.ProductAggregateJson.Hydrate(products.Values, _serializer);

        return payloads
            .Select(item => ToCombo(item.Promotion, item.Payload, products))
            .ToList();
    }

    private static Combo ToCombo(
        Promotion promotion,
        ComboPayload payload,
        IReadOnlyDictionary<int, Product> products)
    {
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

        combo.Items = payload.Items
            .OrderBy(item => item.SortOrder)
            .Select((item, index) =>
            {
                products.TryGetValue(item.ProductId, out var product);
                var variant = product?.Variants.FirstOrDefault(candidate =>
                    candidate.Id == item.ProductVariantId);
                return new ComboItem
                {
                    Id = index + 1,
                    ComboId = combo.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    SortOrder = item.SortOrder,
                    Product = product!,
                    ProductVariant = variant
                };
            })
            .ToList();
        return combo;
    }

    private static int ReadRevision(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("revision", out var revision)
                && revision.TryGetInt32(out var value)
                ? value
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string? ReadDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("details", out var details)
                ? details.GetString()
                : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private async Task AddAuditAsync(Combo combo, int? adminId, string action, string details)
    {
        if (_auditLogWriter == null)
            return;

        var newValue = JsonSerializer.Serialize(new { revision = combo.Revision, details });
        await _auditLogWriter.WriteAsync(
            action,
            "Combo",
            combo.Id,
            adminId ?? 0,
            newValue: newValue);
    }

    private async Task<ComboResult> RunAtomicWriteAsync(Func<Task<ComboResult>> operation)
    {
        if (_dbContext == null)
            return ComboResult.Fail("Dịch vụ combo chưa được cấu hình.");
        if ((_unitOfWork.DatabaseProviderName ?? string.Empty).Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return await operation();

        await using var transaction = await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var result = await operation();
            if (result.Success)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ComboResult.Fail("Combo đã được người khác cập nhật. Vui lòng tải lại trang.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> ValidateComboAsync(ComboFormViewModel model)
    {
        if (!Enum.IsDefined(model.Status))
            return "Trạng thái combo không hợp lệ.";
        if (model.Status == ComboLifecycleStatus.Scheduled && !model.StartsAt.HasValue)
            return "Combo hẹn lịch phải có thời gian bắt đầu bán.";
        if (model.StartsAt.HasValue && model.EndsAt.HasValue && model.EndsAt <= model.StartsAt)
            return "Thời gian kết thúc phải sau thời gian bắt đầu.";
        if (string.IsNullOrWhiteSpace(model.Name))
            return "Tên combo không được để trống.";
        if (model.Items == null || model.Items.Count < 2)
            return "Combo phải có ít nhất 2 sản phẩm.";
        if (model.Items.Count > 20)
            return "Combo không được vượt quá 20 sản phẩm.";
        if (model.Items.Any(item => item.ProductId <= 0 || item.Quantity <= 0))
            return "Sản phẩm và số lượng trong combo không hợp lệ.";

        var duplicate = model.Items
            .GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            return "Combo không được chứa sản phẩm hoặc biến thể trùng nhau.";

        var productIds = model.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _unitOfWork.Products.Query()
            .Where(product => productIds.Contains(product.Id))
            .Include(product => product.Variants)
            .ToDictionaryAsync(product => product.Id);

        if (products.Count != productIds.Count)
            return "Một số sản phẩm không tồn tại.";

        foreach (var item in model.Items)
        {
            var product = products[item.ProductId];
            if (!product.IsActive || product.IsDeleted)
                return $"Sản phẩm '{product.Name}' đã ngừng bán.";

            var minimumStep = string.Equals(product.Unit?.Trim(), "kg", StringComparison.OrdinalIgnoreCase)
                ? 0.1m
                : 1m;
            if (!QuantityRules.IsValid(product.Unit, item.Quantity, minimumStep))
                return $"Số lượng của '{product.Name}' không hợp lệ.";

            var activeVariants = product.Variants.Where(variant => variant.IsActive).ToList();
            var selectedVariant = item.ProductVariantId.HasValue
                ? activeVariants.FirstOrDefault(variant => variant.Id == item.ProductVariantId.Value)
                : null;

            if (activeVariants.Count > 0 && selectedVariant == null)
                return $"Vui lòng chọn biến thể đang bán cho '{product.Name}'.";
            if (activeVariants.Count == 0 && item.ProductVariantId.HasValue)
                return $"Biến thể đã chọn không thuộc sản phẩm '{product.Name}'.";

            if (model.Status is ComboLifecycleStatus.Active or ComboLifecycleStatus.Scheduled)
            {
                var stock = selectedVariant?.StockQuantity ?? product.StockQuantity;
                if (stock < item.Quantity)
                    return $"'{product.Name}' không đủ tồn kho để kích hoạt combo.";
            }
        }

        var targets = model.Items
            .Select(item => new PriceTargetKey(item.ProductId, item.ProductVariantId))
            .ToList();
        var quotes = await _pricing.GetQuotesAsync(targets);
        if (targets.Any(target => !quotes.ContainsKey(target)))
            return "Không thể xác định giá của một số sản phẩm trong combo.";

        var originalTotal = model.Items.Sum(item =>
            quotes[new PriceTargetKey(item.ProductId, item.ProductVariantId)].EffectivePrice * item.Quantity);
        if (!Enum.IsDefined(model.PricingType))
            return "Cách tính giá combo không hợp lệ.";
        if (model.PricingType == ComboPricingType.FixedPrice &&
            (!model.FixedPrice.HasValue || model.FixedPrice <= 0 || model.FixedPrice > originalTotal))
            return "Giá combo cố định phải lớn hơn 0 và không vượt quá tổng giá sản phẩm.";
        if (model.PricingType == ComboPricingType.PercentageDiscount &&
            (!model.DiscountValue.HasValue || model.DiscountValue <= 0 || model.DiscountValue >= 100))
            return "Phần trăm giảm phải lớn hơn 0 và nhỏ hơn 100.";
        if (model.PricingType == ComboPricingType.FixedDiscount &&
            (!model.DiscountValue.HasValue || model.DiscountValue <= 0 || model.DiscountValue >= originalTotal))
            return "Số tiền giảm phải lớn hơn 0 và nhỏ hơn tổng giá sản phẩm.";

        return null;
    }

    private string GetUnavailableReason(ComboItem item, IReadOnlyDictionary<PriceTargetKey, PriceQuote> quotes)
    {
        if (item.Product == null)
            return "không tồn tại";
        if (item.ProductVariantId.HasValue && item.ProductVariant == null)
            return "biến thể không tồn tại";

        var key = new PriceTargetKey(item.ProductId, item.ProductVariantId);
        if (!quotes.ContainsKey(key))
            return "tạm hết";

        var stock = item.ProductVariant?.StockQuantity ?? item.Product?.StockQuantity ?? 0;
        if (item.Quantity > stock)
            return "không đủ tồn kho";

        return string.Empty;
    }

    private static List<ComboItemPayload> BuildPayloadItems(List<ComboItemFormModel> items)
    {
        return items
            .OrderBy(i => i.SortOrder)
            .Select((item, index) => new ComboItem
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                SortOrder = index
            })
            .Select(item => new ComboItemPayload
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                SortOrder = item.SortOrder
            })
            .ToList();
    }

    private async Task<string?> ResolveSlugAsync(string? requestedSlug, string name, int? excludeId = null)
    {
        var slug = string.IsNullOrWhiteSpace(requestedSlug) ? GenerateSlug(name) : GenerateSlug(requestedSlug);
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        if (_dbContext == null)
            return slug;

        var promotions = await _dbContext.Promotions.AsNoTracking()
            .Where(item => item.Type == "combo")
            .ToListAsync();
        var exists = promotions.Any(promotion =>
        {
            if (excludeId.HasValue && promotion.Id == excludeId.Value)
                return false;
            return _serializer.Deserialize<ComboPayload>(promotion.PayloadJson).Slug == slug;
        });

        return exists ? null : slug;
    }

    private async Task SyncLegacyComboAsync(Promotion promotion, ComboPayload payload)
    {
        if (_dbContext == null)
            return;

        Combo? legacy = null;
        if (TryReadLegacyId(promotion.Code, out var legacyId))
        {
            legacy = await _dbContext.Combos
                .Include(combo => combo.Items)
                .FirstOrDefaultAsync(combo => combo.Id == legacyId);
        }

        var createdLegacy = legacy == null;
        if (createdLegacy)
        {
            legacy = new Combo();
            _dbContext.Combos.Add(legacy);
        }

        var legacyCombo = legacy!;
        legacyCombo.Name = payload.Name;
        legacyCombo.Slug = payload.Slug;
        legacyCombo.Description = payload.Description;
        legacyCombo.ImageUrl = payload.ImageUrl;
        legacyCombo.IsActive = payload.IsActive;
        legacyCombo.Status = payload.Status;
        legacyCombo.StartsAt = payload.StartsAt;
        legacyCombo.EndsAt = payload.EndsAt;
        legacyCombo.PricingType = payload.PricingType;
        legacyCombo.FixedPrice = payload.FixedPrice;
        legacyCombo.DiscountValue = payload.DiscountValue;
        legacyCombo.AllowCouponStacking = payload.AllowCouponStacking;
        legacyCombo.Revision = payload.Revision;
        legacyCombo.SortOrder = payload.SortOrder;
        legacyCombo.UpdatedAt = promotion.UpdatedAt;
        if (legacyCombo.CreatedAt == default)
            legacyCombo.CreatedAt = promotion.CreatedAt;

        legacyCombo.Items.Clear();
        legacyCombo.Items = payload.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new ComboItem
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                SortOrder = item.SortOrder
            })
            .ToList();
        await _dbContext.SaveChangesAsync();

        if (createdLegacy)
        {
            promotion.Code = $"combo:{legacyCombo.Id}";
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<int> ResolveLegacyComboIdAsync(int promotionId)
    {
        if (_dbContext == null)
            return promotionId;

        var promotion = await _dbContext.Promotions.AsNoTracking()
            .Where(item => item.Id == promotionId && item.Type == "combo")
            .Select(item => new { item.Code })
            .FirstOrDefaultAsync();
        if (promotion != null && TryReadLegacyId(promotion.Code, out var legacyId) &&
            await _dbContext.Combos.AnyAsync(combo => combo.Id == legacyId))
            return legacyId;
        return promotionId;
    }

    private static bool TryReadLegacyId(string? code, out int id)
    {
        id = 0;
        return code != null && code.StartsWith("combo:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code["combo:".Length..], out id) && id > 0;
    }

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = name.ToLowerInvariant();
        slug = slug.Replace("đ", "d").Replace("Đ", "d");
        slug = Regex.Replace(slug, "[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        slug = Regex.Replace(slug, "[èéẹẻẽêềếệểễ]", "e");
        slug = Regex.Replace(slug, "[ìíịỉĩ]", "i");
        slug = Regex.Replace(slug, "[òóọỏõôồốộổỗơờớợởỡ]", "o");
        slug = Regex.Replace(slug, "[ùúụủũưừứựửữ]", "u");
        slug = Regex.Replace(slug, "[ỳýỵỷỹ]", "y");
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "combo" : slug;
    }
}
