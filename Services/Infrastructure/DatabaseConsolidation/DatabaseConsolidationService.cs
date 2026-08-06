using System.Globalization;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Models.Returns;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fruitables.Services.Infrastructure.DatabaseConsolidation;

public sealed class DatabaseConsolidationService : IDatabaseConsolidationService
{
    private static readonly JsonSerializerOptions WishlistJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IJsonDocumentSerializer _serializer;
    private readonly ILogger<DatabaseConsolidationService> _logger;

    public DatabaseConsolidationService(
        ApplicationDbContext db,
        IJsonDocumentSerializer serializer,
        ILogger<DatabaseConsolidationService>? logger = null)
    {
        _db = db;
        _serializer = serializer;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseConsolidationService>.Instance;
    }

    public async Task<ConsolidationReport> BackfillAsync(bool apply, CancellationToken cancellationToken)
    {
        var report = new ConsolidationReport(apply);

        await RunStageAsync(report, "Products", BackfillProductsAsync, cancellationToken);
        await RunStageAsync(report, "Roles", BackfillRolesAsync, cancellationToken);
        await RunStageAsync(report, "Users", BackfillUsersAsync, cancellationToken);
        await RunStageAsync(report, "Carts", BackfillCartsAsync, cancellationToken);
        await RunStageAsync(report, "Orders", BackfillOrdersAsync, cancellationToken);
        await RunStageAsync(report, "Payments", BackfillPaymentsAsync, cancellationToken);
        await RunStageAsync(report, "Promotions", BackfillPromotionsAsync, cancellationToken);
        await RunStageAsync(report, "Reviews", BackfillReviewsAsync, cancellationToken);
        await RunStageAsync(report, "Returns", BackfillReturnsAsync, cancellationToken);
        await RunStageAsync(report, "ContentEntries", BackfillContentEntriesAsync, cancellationToken);
        await RunStageAsync(report, "ChatSessions", BackfillChatSessionsAsync, cancellationToken);
        await RunStageAsync(report, "AuditLogs", BackfillAuditLogsAsync, cancellationToken);

        return report;
    }

    public async Task<ConsolidationVerificationReport> VerifyAsync(CancellationToken cancellationToken)
    {
        var report = new ConsolidationVerificationReport();

        await RunVerificationStageAsync(report, "Products", VerifyProductsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Roles", VerifyRolesAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Users", VerifyUsersAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Carts", VerifyCartsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Orders", VerifyOrdersAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Payments", VerifyPaymentsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Promotions", VerifyPromotionsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Reviews", VerifyReviewsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "Returns", VerifyReturnsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "ContentEntries", VerifyContentEntriesAsync, cancellationToken);
        await RunVerificationStageAsync(report, "ChatSessions", VerifyChatSessionsAsync, cancellationToken);
        await RunVerificationStageAsync(report, "AuditLogs", VerifyAuditLogsAsync, cancellationToken);

        return report;
    }

    private async Task RunVerificationStageAsync(
        ConsolidationVerificationReport report,
        string stage,
        Func<ConsolidationVerificationReport, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(report, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database consolidation verification stage {Stage} failed", stage);
            report.AddError(stage, $"stage:{stage}", exception.Message, exception.GetType().Name);
        }
    }

    private async Task RunStageAsync(
        ConsolidationReport report,
        string stage,
        Func<ConsolidationReport, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(report, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database consolidation stage {Stage} failed", stage);
            report.AddError(new ConsolidationError(
                stage,
                $"stage:{stage}",
                exception.Message,
                exception.GetType().Name));
        }
    }

    private async Task HandleAsync(
        ConsolidationReport report,
        string aggregateType,
        string sourceId,
        Func<Task<bool>> operation)
    {
        report.Planned++;
        try
        {
            var changed = await operation();
            if (!report.Applied)
                return;

            if (changed)
                report.Processed++;
            else
                report.Skipped++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database consolidation failed for {AggregateType} {SourceId}", aggregateType, sourceId);
            report.AddError(new ConsolidationError(
                aggregateType,
                sourceId,
                exception.Message,
                exception.GetType().Name));
        }
    }

    private async Task BackfillProductsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Images)
            .Include(product => product.Tags)
            .OrderBy(product => product.Id)
            .ToListAsync(cancellationToken);

        foreach (var product in products)
        {
            var sourceId = SourceId("Product", product.Id);
            await HandleAsync(report, "Product", sourceId, async () =>
            {
                var images = product.Images
                    .OrderBy(image => image.SortOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => new ProductImageDocument
                    {
                        Url = RequireText(image.ImageUrl, $"ProductImage:{image.Id}.ImageUrl"),
                        StorageKey = RequireText(image.ImageUrl, $"ProductImage:{image.Id}.StorageKey").TrimStart('/'),
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList();
                var tags = product.Tags
                    .OrderBy(tag => tag.Id)
                    .Select(tag => new ProductTagDocument
                    {
                        Name = RequireText(tag.Name, $"ProductTag:{tag.Id}.Name"),
                        Slug = RequireText(tag.Slug, $"ProductTag:{tag.Id}.Slug")
                    })
                    .ToList();

                var imagesJson = _serializer.Serialize(new ProductImagesDocument { Images = images });
                var tagsJson = _serializer.Serialize(new ProductTagsDocument { Tags = tags });
                return await UpdateProductJsonAsync(product.Id, imagesJson, tagsJson, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillRolesAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(role => role.Id).ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            var sourceId = SourceId("Role", role.Id);
            await HandleAsync(report, "RolePermissions", sourceId, async () =>
            {
                var mappings = await _db.RolePermissions
                    .AsNoTracking()
                    .Include(mapping => mapping.Permission)
                    .Where(mapping => mapping.RoleId == role.Id)
                    .OrderBy(mapping => mapping.Id)
                    .ToListAsync(cancellationToken);

                var permissions = mappings.Select(mapping => new RolePermissionEntry
                {
                    PermissionId = mapping.PermissionId,
                    PermissionName = RequireText(mapping.Permission?.Name, $"RolePermission:{mapping.Id}.PermissionName"),
                    AssignedAt = RequireDate(mapping.AssignedAt, $"RolePermission:{mapping.Id}.AssignedAt"),
                    AssignedByAdminId = mapping.AssignedByAdminId
                }).ToList();

                var json = _serializer.Serialize(new RolePermissionsDocument
                {
                    RoleId = role.Id,
                    Permissions = permissions
                });
                return await UpdateRoleJsonAsync(role.Id, json, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillUsersAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(user => user.Id).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            var sourceId = SourceId("User", user.Id);
            await HandleAsync(report, "UserRoles", sourceId, async () =>
            {
                var mappings = await _db.UserRoleMappings
                    .AsNoTracking()
                    .Include(mapping => mapping.Role)
                    .Where(mapping => mapping.UserId == user.Id)
                    .OrderBy(mapping => mapping.Id)
                    .ToListAsync(cancellationToken);
                var roles = mappings.Select(mapping => new UserRoleEntry
                {
                    RoleId = mapping.RoleId,
                    RoleName = RequireText(mapping.Role?.Name, $"UserRoleMapping:{mapping.Id}.RoleName"),
                    AssignedAt = RequireDate(mapping.AssignedAt, $"UserRoleMapping:{mapping.Id}.AssignedAt"),
                    AssignedByAdminId = mapping.AssignedByAdminId
                }).ToList();
                var rolesJson = _serializer.Serialize(new UserRolesDocument
                {
                    UserId = user.Id,
                    Roles = roles
                });

                var wishlist = await _db.Wishlists
                    .AsNoTracking()
                    .Where(item => item.UserId == user.Id)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);
                foreach (var item in wishlist)
                {
                    if (!await ProductExistsAsync(item.ProductId, cancellationToken))
                        throw new InvalidOperationException($"Wishlist {item.Id} references missing product {item.ProductId}.");
                }

                var wishlistJson = JsonSerializer.Serialize(
                    wishlist.Select(item => item.ProductId).ToArray(),
                    WishlistJsonOptions);
                return await UpdateUserJsonAsync(user.Id, rolesJson, wishlistJson, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillCartsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var carts = await _db.Carts.AsNoTracking().OrderBy(cart => cart.Id).ToListAsync(cancellationToken);
        foreach (var cart in carts)
        {
            var sourceId = SourceId("Cart", cart.Id);
            await HandleAsync(report, "Cart", sourceId, async () =>
            {
                var items = await _db.CartItems
                    .AsNoTracking()
                    .Where(item => item.CartId == cart.Id)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);
                var groups = (await _db.CartGroups
                    .AsNoTracking()
                    .Where(group => group.CartId == cart.Id)
                    .Select(group => group.Id)
                    .ToListAsync(cancellationToken)).ToHashSet();

                var lines = new List<CartLineDocument>(items.Count);
                foreach (var item in items)
                {
                    await RequireProductReferenceAsync(item.ProductId, item.ProductVariantId, $"CartItem:{item.Id}", cancellationToken);
                    if (item.CartGroupId.HasValue && !groups.Contains(item.CartGroupId.Value))
                        throw new InvalidOperationException($"CartItem {item.Id} references missing cart group {item.CartGroupId.Value}.");
                    lines.Add(new CartLineDocument
                    {
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        CartGroupId = item.CartGroupId,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        ComboDiscount = item.ComboDiscount
                    });
                }

                var json = _serializer.Serialize(new CartLinesDocument { Lines = lines });
                return await UpdateCartJsonAsync(cart.Id, json, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillOrdersAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders.AsNoTracking().OrderBy(order => order.Id).ToListAsync(cancellationToken);
        foreach (var order in orders)
        {
            var sourceId = SourceId("Order", order.Id);
            await HandleAsync(report, "OrderHistory", sourceId, async () =>
            {
                var histories = await _db.OrderStatusHistories
                    .AsNoTracking()
                    .Where(history => history.OrderId == order.Id)
                    .OrderBy(history => history.Id)
                    .ToListAsync(cancellationToken);
                var entries = new List<OrderStatusHistoryEntry>(histories.Count);
                foreach (var history in histories)
                {
                    await RequireUserReferenceAsync(history.AdminId, $"OrderStatusHistory:{history.Id}.AdminId", cancellationToken);
                    entries.Add(new OrderStatusHistoryEntry
                    {
                        OldStatus = history.OldStatus,
                        NewStatus = history.NewStatus,
                        AdminId = history.AdminId,
                        Notes = history.Notes,
                        CreatedAt = RequireDate(history.CreatedAt, $"OrderStatusHistory:{history.Id}.CreatedAt")
                    });
                }

                var notes = await _db.OrderNotes
                    .AsNoTracking()
                    .Where(note => note.OrderId == order.Id)
                    .OrderBy(note => note.Id)
                    .ToListAsync(cancellationToken);
                var noteDocuments = new List<OrderNoteDocument>(notes.Count);
                foreach (var note in notes)
                {
                    await RequireUserReferenceAsync(note.AdminId, $"OrderNote:{note.Id}.AdminId", cancellationToken);
                    noteDocuments.Add(new OrderNoteDocument
                    {
                        AdminId = note.AdminId,
                        AdminName = RequireText(note.AdminName, $"OrderNote:{note.Id}.AdminName"),
                        Content = RequireText(note.Content, $"OrderNote:{note.Id}.Content"),
                        CreatedAt = RequireDate(note.CreatedAt, $"OrderNote:{note.Id}.CreatedAt")
                    });
                }

                var historyJson = _serializer.Serialize(new OrderStatusHistoryDocument { Entries = entries });
                var notesJson = _serializer.Serialize(new OrderNotesDocument { Notes = noteDocuments });
                return await UpdateOrderJsonAsync(order.Id, historyJson, notesJson, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillPaymentsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var transactions = await _db.SePayTransactions
            .AsNoTracking()
            .OrderBy(transaction => transaction.Id)
            .ToListAsync(cancellationToken);
        foreach (var transaction in transactions)
        {
            var sourceId = SourceId("SePayTransaction", transaction.Id);
            await HandleAsync(report, "Payment", sourceId, async () =>
            {
                if (!transaction.OrderId.HasValue)
                    throw new InvalidOperationException($"SePay transaction {transaction.Id} has no order reference.");
                await RequireOrderReferenceAsync(transaction.OrderId.Value, sourceId, cancellationToken);
                var providerTransactionId = transaction.SePayTransactionId.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(providerTransactionId))
                    throw new InvalidOperationException($"SePay transaction {transaction.Id} has no transaction ID.");

                var payment = new Payment
                {
                    OrderId = transaction.OrderId.Value,
                    Provider = "SePay",
                    ProviderTransactionId = providerTransactionId,
                    Amount = transaction.TransferAmount,
                    Status = transaction.Status == SePayTransactionStatus.Paid
                        ? PaymentStatus.Paid
                        : PaymentStatus.Pending,
                    PaymentCode = transaction.PaymentCode,
                    ReferenceCode = transaction.ReferenceCode,
                    Message = transaction.Message,
                    CreatedAtUtc = transaction.CreatedAt,
                    PaidAtUtc = transaction.Status == SePayTransactionStatus.Paid ? transaction.CreatedAt : null,
                    UpdatedAtUtc = transaction.CreatedAt
                };
                return await UpsertPaymentAsync(payment, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillPromotionsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var coupons = await _db.Coupons.AsNoTracking().OrderBy(coupon => coupon.Id).ToListAsync(cancellationToken);
        foreach (var coupon in coupons)
        {
            var sourceId = SourceId("Coupon", coupon.Id);
            await HandleAsync(report, "Promotion", sourceId, async () =>
            {
                var payload = new CouponPayload
                {
                    Code = RequireText(coupon.Code, $"Coupon:{coupon.Id}.Code"),
                    Type = coupon.Type,
                    Value = coupon.Value,
                    MinOrderAmount = coupon.MinOrderAmount,
                    MinQuantity = coupon.MinQuantity,
                    MaxUses = coupon.MaxUses,
                    UsedCount = coupon.UsedCount,
                    StartDate = coupon.StartDate,
                    EndDate = coupon.EndDate,
                    IsActive = coupon.IsActive
                };
                var promotion = new Promotion
                {
                    Type = "coupon",
                    Code = PromotionKey("coupon", coupon.Id),
                    PayloadJson = _serializer.Serialize(payload),
                    IsActive = coupon.IsActive,
                    StartsAt = ToOffset(coupon.StartDate),
                    EndsAt = ToOffset(coupon.EndDate),
                    Revision = 1,
                    CreatedAt = LegacyPromotionTimestamp(coupon.Id),
                    UpdatedAt = LegacyPromotionTimestamp(coupon.Id)
                };
                return await UpsertPromotionAsync(promotion, report.Applied, cancellationToken);
            });
        }

        var combos = await _db.Combos.AsNoTracking().OrderBy(combo => combo.Id).ToListAsync(cancellationToken);
        foreach (var combo in combos)
        {
            var sourceId = SourceId("Combo", combo.Id);
            await HandleAsync(report, "Promotion", sourceId, async () =>
            {
                var items = await _db.ComboItems
                    .AsNoTracking()
                    .Where(item => item.ComboId == combo.Id)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Id)
                    .ToListAsync(cancellationToken);
                var payloadItems = new List<ComboItemPayload>(items.Count);
                foreach (var item in items)
                {
                    await RequireProductReferenceAsync(item.ProductId, item.ProductVariantId, $"ComboItem:{item.Id}", cancellationToken);
                    payloadItems.Add(new ComboItemPayload
                    {
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        Quantity = item.Quantity,
                        SortOrder = item.SortOrder
                    });
                }

                var payload = new ComboPayload
                {
                    Name = RequireText(combo.Name, $"Combo:{combo.Id}.Name"),
                    Slug = RequireText(combo.Slug, $"Combo:{combo.Id}.Slug"),
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
                    Revision = combo.Revision,
                    SortOrder = combo.SortOrder,
                    Items = payloadItems
                };
                var promotion = new Promotion
                {
                    Type = "combo",
                    Code = PromotionKey("combo", combo.Id),
                    PayloadJson = _serializer.Serialize(payload),
                    IsActive = combo.IsActive,
                    StartsAt = combo.StartsAt,
                    EndsAt = combo.EndsAt,
                    Revision = combo.Revision,
                    CreatedAt = combo.CreatedAt,
                    UpdatedAt = combo.UpdatedAt
                };
                return await UpsertPromotionAsync(promotion, report.Applied, cancellationToken);
            });
        }

        var schedules = await _db.PriceSchedules.AsNoTracking().OrderBy(schedule => schedule.Id).ToListAsync(cancellationToken);
        foreach (var schedule in schedules)
        {
            var sourceId = SourceId("PriceSchedule", schedule.Id);
            await HandleAsync(report, "Promotion", sourceId, async () =>
            {
                await RequireProductReferenceAsync(schedule.ProductId, schedule.ProductVariantId, sourceId, cancellationToken);
                var payload = new PriceSchedulePayload
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
                var promotion = new Promotion
                {
                    Type = "price-schedule",
                    Code = PromotionKey("price-schedule", schedule.Id),
                    PayloadJson = _serializer.Serialize(payload),
                    IsActive = !schedule.IsCancelled,
                    StartsAt = schedule.StartsAt,
                    EndsAt = schedule.EndsAt,
                    Revision = schedule.Revision,
                    CreatedAt = schedule.CreatedAt.UtcDateTime,
                    UpdatedAt = schedule.UpdatedAt.UtcDateTime
                };
                return await UpsertPromotionAsync(promotion, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillReviewsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var reviews = await _db.Reviews.AsNoTracking().OrderBy(review => review.Id).ToListAsync(cancellationToken);
        foreach (var review in reviews)
        {
            var sourceId = SourceId("Review", review.Id);
            await HandleAsync(report, "ReviewMetadata", sourceId, async () =>
            {
                await RequireProductReferenceAsync(review.ProductId, null, sourceId, cancellationToken);
                await RequireUserReferenceAsync(review.UserId, sourceId, cancellationToken);
                var payload = new ReviewMetadataDocument
                {
                    Status = review.Status,
                    IsHidden = review.IsHidden,
                    HiddenReason = review.HiddenReason,
                    HiddenByAdminId = review.HiddenByAdminId,
                    HiddenAt = review.HiddenAt,
                    IsDeleted = review.IsDeleted,
                    DeletedByAdminId = review.DeletedByAdminId,
                    DeletedAt = review.DeletedAt,
                    IsVerifiedPurchase = review.IsVerifiedPurchase,
                    HelpfulCount = review.HelpfulCount,
                    ReportCount = review.ReportCount,
                    CreatedAt = RequireDate(review.CreatedAt, sourceId),
                    UpdatedAt = review.UpdatedAt
                };
                var json = _serializer.Serialize(payload);
                return await UpdateReviewJsonAsync(review.Id, json, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillReturnsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var requests = await _db.ReturnRequests.AsNoTracking().OrderBy(request => request.Id).ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            var sourceId = SourceId("ReturnRequest", request.Id);
            await HandleAsync(report, "Return", sourceId, async () =>
            {
                await RequireOrderReferenceAsync(request.OrderId, sourceId, cancellationToken);
                await RequireUserReferenceAsync(request.UserId, sourceId, cancellationToken);

                var sourceItems = await _db.ReturnRequestItems
                    .AsNoTracking()
                    .Where(item => item.ReturnRequestId == request.Id)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);
                var itemDocuments = new List<ReturnItemDetails>(sourceItems.Count);
                foreach (var item in sourceItems)
                {
                    var orderItem = await _db.OrderItems
                        .AsNoTracking()
                        .SingleOrDefaultAsync(orderItem => orderItem.Id == item.OrderItemId, cancellationToken);
                    if (orderItem is null || orderItem.OrderId != request.OrderId)
                        throw new InvalidOperationException($"ReturnRequestItem {item.Id} references an invalid order item.");
                    itemDocuments.Add(new ReturnItemDetails
                    {
                        OrderItemId = item.OrderItemId,
                        DecisionStatus = item.DecisionStatus,
                        RequestedQuantity = item.RequestedQuantity,
                        ApprovedQuantity = item.ApprovedQuantity,
                        Reason = item.Reason,
                        Description = RequireText(item.Description, $"ReturnRequestItem:{item.Id}.Description"),
                        DecisionReason = item.DecisionReason,
                        RequestedAmount = item.RequestedAmount,
                        ApprovedAmount = item.ApprovedAmount
                    });
                }

                var sourceEvidence = await _db.ReturnEvidence
                    .AsNoTracking()
                    .Where(evidence => evidence.ReturnRequestId == request.Id)
                    .OrderBy(evidence => evidence.Id)
                    .ToListAsync(cancellationToken);
                var evidenceDocuments = new List<ReturnEvidenceDetails>(sourceEvidence.Count);
                foreach (var evidence in sourceEvidence)
                {
                    await RequireUserReferenceAsync(evidence.UploadedByUserId, $"ReturnEvidence:{evidence.Id}.UploadedByUserId", cancellationToken);
                    if (evidence.ReturnRequestItemId.HasValue
                        && !await ReturnItemExistsAsync(evidence.ReturnRequestItemId.Value, request.Id, cancellationToken))
                        throw new InvalidOperationException($"ReturnEvidence {evidence.Id} references an invalid return item.");
                    evidenceDocuments.Add(new ReturnEvidenceDetails
                    {
                        StorageKey = RequireText(evidence.StorageKey, $"ReturnEvidence:{evidence.Id}.StorageKey"),
                        OriginalFileName = RequireText(evidence.OriginalFileName, $"ReturnEvidence:{evidence.Id}.OriginalFileName"),
                        ContentType = RequireText(evidence.ContentType, $"ReturnEvidence:{evidence.Id}.ContentType"),
                        SizeBytes = evidence.SizeBytes,
                        UploadedByUserId = evidence.UploadedByUserId,
                        UploadedAtUtc = RequireDate(evidence.UploadedAtUtc, $"ReturnEvidence:{evidence.Id}.UploadedAtUtc")
                    });
                }

                var sourceEvents = await _db.ReturnEvents
                    .AsNoTracking()
                    .Where(eventItem => eventItem.ReturnRequestId == request.Id)
                    .OrderBy(eventItem => eventItem.Id)
                    .ToListAsync(cancellationToken);
                var eventDocuments = new List<ReturnEventDetails>(sourceEvents.Count);
                foreach (var eventItem in sourceEvents)
                {
                    if (eventItem.ActorUserId.HasValue)
                        await RequireUserReferenceAsync(eventItem.ActorUserId.Value, $"ReturnEvent:{eventItem.Id}.ActorUserId", cancellationToken);
                    if (eventItem.ReturnRequestItemId.HasValue
                        && !await ReturnItemExistsAsync(eventItem.ReturnRequestItemId.Value, request.Id, cancellationToken))
                        throw new InvalidOperationException($"ReturnEvent {eventItem.Id} references an invalid return item.");
                    eventDocuments.Add(new ReturnEventDetails
                    {
                        OldStatus = eventItem.OldStatus,
                        NewStatus = eventItem.NewStatus,
                        EventType = eventItem.EventType,
                        ActorUserId = eventItem.ActorUserId,
                        Note = eventItem.Note,
                        CreatedAtUtc = RequireDate(eventItem.CreatedAtUtc, $"ReturnEvent:{eventItem.Id}.CreatedAtUtc")
                    });
                }

                RefundDetails? refundDocument = null;
                var refund = await _db.Refunds
                    .AsNoTracking()
                    .SingleOrDefaultAsync(refund => refund.ReturnRequestId == request.Id, cancellationToken);
                if (refund is not null)
                {
                    if (refund.OrderId != request.OrderId)
                        throw new InvalidOperationException($"Refund {refund.Id} references a different order.");
                    await RequireUserReferenceAsync(refund.CreatedByUserId, $"Refund:{refund.Id}.CreatedByUserId", cancellationToken);
                    if (refund.ProcessedByUserId.HasValue)
                        await RequireUserReferenceAsync(refund.ProcessedByUserId.Value, $"Refund:{refund.Id}.ProcessedByUserId", cancellationToken);
                    refundDocument = new RefundDetails
                    {
                        Amount = refund.Amount,
                        ShippingFeeAmount = refund.ShippingFeeAmount,
                        Status = refund.Status,
                        TransactionReference = refund.TransactionReference,
                        FailureReason = refund.FailureReason,
                        CreatedByUserId = refund.CreatedByUserId,
                        ProcessedByUserId = refund.ProcessedByUserId,
                        CreatedAtUtc = RequireDate(refund.CreatedAtUtc, $"Refund:{refund.Id}.CreatedAtUtc"),
                        ProcessedAtUtc = refund.ProcessedAtUtc
                    };
                }

                var detailsJson = _serializer.Serialize(new ReturnDetailsDocument
                {
                    Status = request.Status,
                    SubmittedAtUtc = RequireDate(request.SubmittedAtUtc, sourceId),
                    ClaimDeadlineAtUtc = RequireDate(request.ClaimDeadlineAtUtc, sourceId),
                    SupplementDeadlineAtUtc = request.SupplementDeadlineAtUtc,
                    SupplementCount = request.SupplementCount,
                    RequestedAmount = request.RequestedAmount,
                    ApprovedAmount = request.ApprovedAmount,
                    ApprovedShippingFeeAmount = request.ApprovedShippingFeeAmount,
                    CustomerNote = request.CustomerNote,
                    AdminNote = request.AdminNote,
                    Items = itemDocuments,
                    Evidence = evidenceDocuments,
                    Events = eventDocuments,
                    Refund = refundDocument
                });

                var target = new ReturnCase
                {
                    ReturnNumber = RequireText(request.ReturnNumber, $"ReturnRequest:{request.Id}.ReturnNumber"),
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    Status = request.Status,
                    SubmittedAtUtc = request.SubmittedAtUtc,
                    ClaimDeadlineAtUtc = request.ClaimDeadlineAtUtc,
                    SupplementDeadlineAtUtc = request.SupplementDeadlineAtUtc,
                    SupplementCount = request.SupplementCount,
                    RequestedAmount = request.RequestedAmount,
                    ApprovedAmount = request.ApprovedAmount,
                    ApprovedShippingFeeAmount = request.ApprovedShippingFeeAmount,
                    DetailsJson = detailsJson
                };
                return await UpsertReturnAsync(target, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillContentEntriesAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var faqs = await _db.Faqs.AsNoTracking().OrderBy(faq => faq.Id).ToListAsync(cancellationToken);
        foreach (var faq in faqs)
        {
            var sourceId = SourceId("Faq", faq.Id);
            await HandleAsync(report, "ContentEntry", sourceId, async () =>
            {
                var payload = new ContentPayload
                {
                    Title = RequireText(faq.Title, $"Faq:{faq.Id}.Title"),
                    Body = RequireText(faq.Body, $"Faq:{faq.Id}.Body"),
                    Category = string.IsNullOrWhiteSpace(faq.Category) ? "general" : faq.Category,
                    IsActive = faq.IsActive,
                    CreatedAt = RequireDate(faq.CreatedAt, sourceId),
                    UpdatedAt = RequireDate(faq.UpdatedAt, sourceId)
                };
                var target = new ContentEntry
                {
                    EntryType = "faq",
                    Key = ContentKey("faq", faq.Id),
                    Title = payload.Title,
                    PayloadJson = _serializer.Serialize(payload),
                    IsActive = faq.IsActive,
                    IsRead = false,
                    CreatedAt = faq.CreatedAt,
                    UpdatedAt = faq.UpdatedAt
                };
                return await UpsertContentEntryAsync(target, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillChatSessionsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var sessions = await _db.ChatSessions.AsNoTracking().OrderBy(session => session.Id).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            var sourceId = SourceId("ChatSession", session.Id);
            await HandleAsync(report, "ChatMessages", sourceId, async () =>
            {
                if (session.UserId.HasValue)
                    await RequireUserReferenceAsync(session.UserId.Value, sourceId, cancellationToken);
                var messages = await _db.ChatMessages
                    .AsNoTracking()
                    .Where(message => message.SessionId == session.Id)
                    .OrderBy(message => message.Id)
                    .ToListAsync(cancellationToken);
                var documents = messages.Select(message => new ChatMessageDocument
                {
                    Role = RequireText(message.Role, $"ChatMessage:{message.Id}.Role"),
                    Content = RequireText(message.Content, $"ChatMessage:{message.Id}.Content"),
                    CreatedAt = RequireDate(message.CreatedAt, $"ChatMessage:{message.Id}.CreatedAt"),
                    Metadata = ParseChatMetadata(message.MetaJson, message.Id)
                }).ToList();
                var json = _serializer.Serialize(new ChatMessagesDocument { Messages = documents });
                return await UpdateChatJsonAsync(session.Id, json, report.Applied, cancellationToken);
            });
        }
    }

    private async Task BackfillAuditLogsAsync(ConsolidationReport report, CancellationToken cancellationToken)
    {
        var productLogs = await _db.ProductLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync(cancellationToken);
        foreach (var log in productLogs)
        {
            var sourceId = SourceId("ProductLog", log.Id);
            await HandleAsync(report, "AuditLog", sourceId, async () =>
            {
                await RequireUserReferenceAsync(log.AdminId, sourceId, cancellationToken);
                if (log.ProductId.HasValue)
                    await RequireProductReferenceAsync(log.ProductId.Value, null, sourceId, cancellationToken);
                return await UpsertAuditLogAsync(new AuditLog
                {
                    Action = RequireText(log.Action, sourceId),
                    EntityType = "ProductLog",
                    EntityId = log.Id,
                    ChangedByAdminId = log.AdminId,
                    ChangedAt = RequireDate(log.CreatedAt, sourceId),
                    NewValue = ToAuditJson(log.Details)
                }, report.Applied, cancellationToken);
            });
        }

        var comboLogs = await _db.ComboAuditLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync(cancellationToken);
        foreach (var log in comboLogs)
        {
            var sourceId = SourceId("ComboAuditLog", log.Id);
            await HandleAsync(report, "AuditLog", sourceId, async () =>
            {
                if (!log.AdminId.HasValue)
                    throw new InvalidOperationException($"Combo audit log {log.Id} has no admin reference.");
                await RequireUserReferenceAsync(log.AdminId.Value, sourceId, cancellationToken);
                if (log.ComboId.HasValue && !await ComboExistsAsync(log.ComboId.Value, cancellationToken))
                    throw new InvalidOperationException($"Combo audit log {log.Id} references missing combo {log.ComboId.Value}.");
                return await UpsertAuditLogAsync(new AuditLog
                {
                    Action = RequireText(log.Action, sourceId),
                    EntityType = "ComboAuditLog",
                    EntityId = checked((int)log.Id),
                    ChangedByAdminId = log.AdminId.Value,
                    ChangedAt = RequireDate(log.CreatedAt, sourceId),
                    NewValue = ToAuditJson(log.Details)
                }, report.Applied, cancellationToken);
            });
        }

        var accountLogs = await _db.UserAccountLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync(cancellationToken);
        foreach (var log in accountLogs)
        {
            var sourceId = SourceId("UserAccountLog", log.Id);
            await HandleAsync(report, "AuditLog", sourceId, async () =>
            {
                await RequireUserReferenceAsync(log.UserId, sourceId, cancellationToken);
                await RequireUserReferenceAsync(log.AdminId, sourceId, cancellationToken);
                var newValue = JsonSerializer.Serialize(new
                {
                    log.UserId,
                    log.Action,
                    log.LockType,
                    log.ViolationType,
                    log.Reason,
                    log.ExpiresAt,
                    log.IpAddress,
                    log.UserAgent
                }, WishlistJsonOptions);
                return await UpsertAuditLogAsync(new AuditLog
                {
                    Action = RequireText(log.Action, sourceId),
                    EntityType = "UserAccountLog",
                    EntityId = log.Id,
                    ChangedByAdminId = log.AdminId,
                    ChangedAt = RequireDate(log.CreatedAt, sourceId),
                    NewValue = newValue
                }, report.Applied, cancellationToken);
            });
        }

        var rbacLogs = await _db.RbacAuditLogs.AsNoTracking().OrderBy(log => log.Id).ToListAsync(cancellationToken);
        foreach (var log in rbacLogs)
        {
            var sourceId = SourceId("RbacAuditLog", log.Id);
            await HandleAsync(report, "AuditLog", sourceId, async () =>
            {
                await RequireUserReferenceAsync(log.ChangedByAdminId, sourceId, cancellationToken);
                ValidateOptionalJson(log.OldValue, $"RbacAuditLog:{log.Id}.OldValue");
                ValidateOptionalJson(log.NewValue, $"RbacAuditLog:{log.Id}.NewValue");
                return await UpsertAuditLogAsync(new AuditLog
                {
                    Action = RequireText(log.Action, sourceId),
                    EntityType = "RbacAuditLog",
                    EntityId = log.Id,
                    ChangedByAdminId = log.ChangedByAdminId,
                    ChangedAt = RequireDate(log.ChangedAt, sourceId),
                    OldValue = log.OldValue,
                    NewValue = log.NewValue
                }, report.Applied, cancellationToken);
            });
        }
    }

    private async Task<bool> UpdateProductJsonAsync(int id, string imagesJson, string tagsJson, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Products.AsNoTracking().Where(product => product.Id == id)
            .Select(product => new { product.ImagesJson, product.TagsJson }).SingleAsync(cancellationToken);
        var changed = !string.Equals(current.ImagesJson, imagesJson, StringComparison.Ordinal)
            || !string.Equals(current.TagsJson, tagsJson, StringComparison.Ordinal);
        if (!apply || !changed)
            return changed;
        return await InTransactionAsync(async () =>
        {
            var entity = _db.ChangeTracker.Entries<Product>()
                .Where(entry => entry.Entity.Id == id)
                .Select(entry => entry.Entity)
                .SingleOrDefault();
            if (entity is null)
            {
                entity = new Product { Id = id };
                _db.Products.Attach(entity);
            }
            entity.ImagesJson = imagesJson;
            entity.TagsJson = tagsJson;
            _db.Entry(entity).Property(product => product.ImagesJson).IsModified = true;
            _db.Entry(entity).Property(product => product.TagsJson).IsModified = true;
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpdateRoleJsonAsync(int id, string json, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Roles.AsNoTracking().Where(role => role.Id == id).Select(role => role.PermissionsJson).SingleAsync(cancellationToken);
        if (!apply || string.Equals(current, json, StringComparison.Ordinal))
            return !string.Equals(current, json, StringComparison.Ordinal);
        return await UpdateTrackedPropertyAsync(
            () => _db.Roles.SingleAsync(role => role.Id == id, cancellationToken),
            role => role.PermissionsJson = json,
            cancellationToken);
    }

    private async Task<bool> UpdateUserJsonAsync(int id, string rolesJson, string wishlistJson, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Users.AsNoTracking().Where(user => user.Id == id)
            .Select(user => new { user.RoleIdsJson, user.WishlistJson }).SingleAsync(cancellationToken);
        var changed = !string.Equals(current.RoleIdsJson, rolesJson, StringComparison.Ordinal)
            || !string.Equals(current.WishlistJson, wishlistJson, StringComparison.Ordinal);
        if (!apply || !changed)
            return changed;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.Users.SingleAsync(user => user.Id == id, cancellationToken);
            entity.RoleIdsJson = rolesJson;
            entity.WishlistJson = wishlistJson;
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpdateCartJsonAsync(int id, string json, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Carts.AsNoTracking().Where(cart => cart.Id == id).Select(cart => cart.LinesJson).SingleAsync(cancellationToken);
        if (!apply || string.Equals(current, json, StringComparison.Ordinal))
            return !string.Equals(current, json, StringComparison.Ordinal);
        return await UpdateTrackedPropertyAsync(
            () => _db.Carts.SingleAsync(cart => cart.Id == id, cancellationToken),
            cart => cart.LinesJson = json,
            cancellationToken);
    }

    private async Task<bool> UpdateOrderJsonAsync(int id, string historyJson, string notesJson, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Orders.AsNoTracking().Where(order => order.Id == id)
            .Select(order => new { order.StatusHistoryJson, order.NotesJson }).SingleAsync(cancellationToken);
        var changed = !string.Equals(current.StatusHistoryJson, historyJson, StringComparison.Ordinal)
            || !string.Equals(current.NotesJson, notesJson, StringComparison.Ordinal);
        if (!apply || !changed)
            return changed;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.Orders.SingleAsync(order => order.Id == id, cancellationToken);
            entity.StatusHistoryJson = historyJson;
            entity.NotesJson = notesJson;
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpdateReviewJsonAsync(int id, string json, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.Reviews.AsNoTracking().Where(review => review.Id == id).Select(review => review.MetadataJson).SingleAsync(cancellationToken);
        if (!apply || string.Equals(current, json, StringComparison.Ordinal))
            return !string.Equals(current, json, StringComparison.Ordinal);
        return await UpdateTrackedPropertyAsync(
            () => _db.Reviews.SingleAsync(review => review.Id == id, cancellationToken),
            review => review.MetadataJson = json,
            cancellationToken);
    }

    private async Task<bool> UpdateChatJsonAsync(Guid id, string json, bool apply, CancellationToken cancellationToken)
    {
        var current = await _db.ChatSessions.AsNoTracking().Where(session => session.Id == id).Select(session => session.MessagesJson).SingleAsync(cancellationToken);
        if (!apply || string.Equals(current, json, StringComparison.Ordinal))
            return !string.Equals(current, json, StringComparison.Ordinal);
        return await UpdateTrackedPropertyAsync(
            () => _db.ChatSessions.SingleAsync(session => session.Id == id, cancellationToken),
            session => session.MessagesJson = json,
            cancellationToken);
    }

    private async Task<bool> UpsertPaymentAsync(Payment candidate, bool apply, CancellationToken cancellationToken)
    {
        var existing = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(payment =>
            payment.Provider == candidate.Provider
            && payment.ProviderTransactionId == candidate.ProviderTransactionId, cancellationToken);
        if (existing is not null && SamePayment(existing, candidate))
            return false;
        if (!apply)
            return true;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.Payments.SingleOrDefaultAsync(payment =>
                payment.Provider == candidate.Provider
                && payment.ProviderTransactionId == candidate.ProviderTransactionId, cancellationToken);
            if (entity is null)
                _db.Payments.Add(candidate);
            else
                CopyPayment(entity, candidate);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpsertPromotionAsync(Promotion candidate, bool apply, CancellationToken cancellationToken)
    {
        var existing = await _db.Promotions.AsNoTracking().SingleOrDefaultAsync(promotion =>
            promotion.Type == candidate.Type && promotion.Code == candidate.Code, cancellationToken);
        if (existing is not null && SamePromotion(existing, candidate))
            return false;
        if (!apply)
            return true;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.Promotions.SingleOrDefaultAsync(promotion =>
                promotion.Type == candidate.Type && promotion.Code == candidate.Code, cancellationToken);
            if (entity is null)
                _db.Promotions.Add(candidate);
            else
                CopyPromotion(entity, candidate);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpsertReturnAsync(ReturnCase candidate, bool apply, CancellationToken cancellationToken)
    {
        var existing = await _db.Returns.AsNoTracking().SingleOrDefaultAsync(item => item.OrderId == candidate.OrderId, cancellationToken);
        if (existing is not null && SameReturn(existing, candidate))
            return false;
        if (!apply)
            return true;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.Returns.SingleOrDefaultAsync(item => item.OrderId == candidate.OrderId, cancellationToken);
            if (entity is null)
                _db.Returns.Add(candidate);
            else
                CopyReturn(entity, candidate);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpsertContentEntryAsync(ContentEntry candidate, bool apply, CancellationToken cancellationToken)
    {
        var existing = await _db.ContentEntries.AsNoTracking().SingleOrDefaultAsync(entry =>
            entry.EntryType == candidate.EntryType && entry.Key == candidate.Key, cancellationToken);
        if (existing is not null && SameContentEntry(existing, candidate))
            return false;
        if (!apply)
            return true;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.ContentEntries.SingleOrDefaultAsync(entry =>
                entry.EntryType == candidate.EntryType && entry.Key == candidate.Key, cancellationToken);
            if (entity is null)
                _db.ContentEntries.Add(candidate);
            else
                CopyContentEntry(entity, candidate);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpsertAuditLogAsync(AuditLog candidate, bool apply, CancellationToken cancellationToken)
    {
        var existing = await _db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(log =>
            log.EntityType == candidate.EntityType && log.EntityId == candidate.EntityId, cancellationToken);
        if (existing is not null && SameAuditLog(existing, candidate))
            return false;
        if (!apply)
            return true;
        return await InTransactionAsync(async () =>
        {
            var entity = await _db.AuditLogs.SingleOrDefaultAsync(log =>
                log.EntityType == candidate.EntityType && log.EntityId == candidate.EntityId, cancellationToken);
            if (entity is null)
                _db.AuditLogs.Add(candidate);
            else
                CopyAuditLog(entity, candidate);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> UpdateTrackedPropertyAsync<TEntity>(
        Func<Task<TEntity>> load,
        Action<TEntity> update,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        return await InTransactionAsync(async () =>
        {
            var entity = await load();
            update(entity);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> InTransactionAsync(Func<Task<bool>> mutation, CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            var changed = await mutation();
            if (changed)
                await _db.SaveChangesAsync(cancellationToken);
            return changed;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var changed = await mutation();
            if (changed)
                await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task VerifyProductsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking().OrderBy(product => product.Id).ToListAsync(cancellationToken);
        report.MutableSourceCounts["products"] = products.Count;
        var targetCount = 0;
        foreach (var product in products)
        {
            var validImages = VerifyDocument<ProductImagesDocument>(report, "Product", SourceId("Product", product.Id), product.ImagesJson, _serializer.TryDeserialize<ProductImagesDocument>);
            var validTags = VerifyDocument<ProductTagsDocument>(report, "Product", SourceId("Product", product.Id), product.TagsJson, _serializer.TryDeserialize<ProductTagsDocument>);
            if (validImages && validTags)
            {
                targetCount++;
                var imageDocument = _serializer.Deserialize<ProductImagesDocument>(product.ImagesJson);
                var tagDocument = _serializer.Deserialize<ProductTagsDocument>(product.TagsJson);
                var imageCount = await _db.ProductImages.CountAsync(image => image.ProductId == product.Id, cancellationToken);
                var tagCount = await _db.ProductTags.CountAsync(tag => tag.Products.Any(item => item.Id == product.Id), cancellationToken);
                if (imageDocument.Images.Count != imageCount)
                    report.AddError("Product", SourceId("Product", product.Id), $"Product image count mismatch: source {imageCount}, target {imageDocument.Images.Count}.");
                if (tagDocument.Tags.Count != tagCount)
                    report.AddError("Product", SourceId("Product", product.Id), $"Product tag count mismatch: source {tagCount}, target {tagDocument.Tags.Count}.");
            }

            var reviewCount = await _db.Reviews.CountAsync(review =>
                review.ProductId == product.Id
                && review.Status == ReviewStatus.Approved
                && !review.IsHidden
                && !review.IsDeleted, cancellationToken);
            if (product.ReviewCount != reviewCount)
                report.AddError("Product", SourceId("Product", product.Id), $"Review count mismatch: source summary {product.ReviewCount}, target count {reviewCount}.");
            if (product.StockQuantity < 0)
                report.AddError("Product", SourceId("Product", product.Id), "Product stock is negative.");
        }

        report.MutableTargetCounts["products"] = targetCount;
        CompareCounts(report, "Products", products.Count, targetCount);

        var variants = await _db.ProductVariants.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var variant in variants)
        {
            if (variant.StockQuantity < 0)
                report.AddError("ProductVariant", SourceId("ProductVariant", variant.Id), "Variant stock is negative.");
        }
    }

    private async Task VerifyRolesAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var roles = await _db.Roles.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["roles"] = roles.Count;
        var targetCount = 0;
        foreach (var role in roles)
        {
            var sourceId = SourceId("Role", role.Id);
            if (VerifyDocument<RolePermissionsDocument>(report, "RolePermissions", sourceId, role.PermissionsJson, _serializer.TryDeserialize<RolePermissionsDocument>))
            {
                targetCount++;
                var document = _serializer.Deserialize<RolePermissionsDocument>(role.PermissionsJson);
                var sourceCount = await _db.RolePermissions.CountAsync(mapping => mapping.RoleId == role.Id, cancellationToken);
                if (document.RoleId != role.Id || document.Permissions.Count != sourceCount)
                    report.AddError("RolePermissions", sourceId, $"Role permission count or identity mismatch: source role {role.Id}/{sourceCount}, target role {document.RoleId}/{document.Permissions.Count}.");
            }
        }
        report.MutableTargetCounts["roles"] = targetCount;
        CompareCounts(report, "Roles", roles.Count, targetCount);
    }

    private async Task VerifyUsersAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["users"] = users.Count;
        var targetCount = 0;
        foreach (var user in users)
        {
            var sourceId = SourceId("User", user.Id);
            var rolesValid = VerifyDocument<UserRolesDocument>(report, "UserRoles", sourceId, user.RoleIdsJson, _serializer.TryDeserialize<UserRolesDocument>);
            var wishlistValid = VerifyWishlistJson(report, user);
            if (rolesValid && wishlistValid)
            {
                targetCount++;
                var roleDocument = _serializer.Deserialize<UserRolesDocument>(user.RoleIdsJson);
                var sourceRoleCount = await _db.UserRoleMappings.CountAsync(mapping => mapping.UserId == user.Id, cancellationToken);
                if (roleDocument.UserId != user.Id || roleDocument.Roles.Count != sourceRoleCount)
                    report.AddError("UserRoles", sourceId, $"User role count or identity mismatch: source user {user.Id}/{sourceRoleCount}, target user {roleDocument.UserId}/{roleDocument.Roles.Count}.");

                using var wishlistDocument = JsonDocument.Parse(user.WishlistJson);
                var wishlistCount = await _db.Wishlists.CountAsync(item => item.UserId == user.Id, cancellationToken);
                if (wishlistDocument.RootElement.GetArrayLength() != wishlistCount)
                    report.AddError("UserWishlist", sourceId, $"Wishlist count mismatch: source {wishlistCount}, target {wishlistDocument.RootElement.GetArrayLength()}.");
            }
        }
        report.MutableTargetCounts["users"] = targetCount;
        CompareCounts(report, "Users", users.Count, targetCount);
    }

    private async Task VerifyCartsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var carts = await _db.Carts.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["carts"] = carts.Count;
        var targetCount = 0;
        foreach (var cart in carts)
        {
            var sourceId = SourceId("Cart", cart.Id);
            if (VerifyDocument<CartLinesDocument>(report, "Cart", sourceId, cart.LinesJson, _serializer.TryDeserialize<CartLinesDocument>))
            {
                targetCount++;
                var document = _serializer.Deserialize<CartLinesDocument>(cart.LinesJson);
                var sourceCount = await _db.CartItems.CountAsync(item => item.CartId == cart.Id, cancellationToken);
                if (document.Lines.Count != sourceCount)
                    report.AddError("Cart", sourceId, $"Cart line count mismatch: source {sourceCount}, target {document.Lines.Count}.");
            }
        }
        report.MutableTargetCounts["carts"] = targetCount;
        CompareCounts(report, "Carts", carts.Count, targetCount);
    }

    private async Task VerifyOrdersAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["orders"] = orders.Count;
        var targetCount = 0;
        foreach (var order in orders)
        {
            var sourceId = SourceId("Order", order.Id);
            var historyValid = VerifyDocument<OrderStatusHistoryDocument>(report, "OrderHistory", sourceId, order.StatusHistoryJson, _serializer.TryDeserialize<OrderStatusHistoryDocument>);
            var notesValid = VerifyDocument<OrderNotesDocument>(report, "OrderNotes", sourceId, order.NotesJson, _serializer.TryDeserialize<OrderNotesDocument>);
            if (historyValid && notesValid)
            {
                targetCount++;
                var historyDocument = _serializer.Deserialize<OrderStatusHistoryDocument>(order.StatusHistoryJson);
                var notesDocument = _serializer.Deserialize<OrderNotesDocument>(order.NotesJson);
                var sourceHistoryCount = await _db.OrderStatusHistories.CountAsync(history => history.OrderId == order.Id, cancellationToken);
                var sourceNoteCount = await _db.OrderNotes.CountAsync(note => note.OrderId == order.Id, cancellationToken);
                if (historyDocument.Entries.Count != sourceHistoryCount)
                    report.AddError("OrderHistory", sourceId, $"Order status history count mismatch: source {sourceHistoryCount}, target {historyDocument.Entries.Count}.");
                if (notesDocument.Notes.Count != sourceNoteCount)
                    report.AddError("OrderNotes", sourceId, $"Order note count mismatch: source {sourceNoteCount}, target {notesDocument.Notes.Count}.");
            }

            var items = await _db.OrderItems.AsNoTracking().Where(item => item.OrderId == order.Id).ToListAsync(cancellationToken);
            var itemSubtotal = items.Sum(item => item.Total);
            if (itemSubtotal != order.Subtotal)
                report.AddError("Order", sourceId, $"Order total item subtotal mismatch: source {order.Subtotal}, target {itemSubtotal}.");
            if (items.Any(item => item.Quantity <= 0))
                report.AddError("Order", sourceId, "Order item quantity must be positive.");
            var expectedTotal = order.Subtotal + order.ShippingFee - order.Discount;
            if (expectedTotal != order.Total)
                report.AddError("Order", sourceId, $"Order total mismatch: source total {order.Total}, target total {expectedTotal}.");
        }
        report.MutableTargetCounts["orders"] = targetCount;
        CompareCounts(report, "Orders", orders.Count, targetCount);
    }

    private async Task VerifyPaymentsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var transactions = await _db.SePayTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var payments = await _db.Payments.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["payments"] = transactions.Count;
        report.MutableTargetCounts["payments"] = payments.Count;
        CompareCounts(report, "Payments", transactions.Count, payments.Count);
        foreach (var transaction in transactions)
        {
            var sourceId = $"Payment:SePay:{transaction.SePayTransactionId.ToString(CultureInfo.InvariantCulture)}";
            var providerTransactionId = transaction.SePayTransactionId.ToString(CultureInfo.InvariantCulture);
            if (!payments.Any(payment => payment.Provider == "SePay" && payment.ProviderTransactionId == providerTransactionId))
                report.AddError("Payment", sourceId, $"Payment transaction ID {providerTransactionId} is missing from target.");
        }
    }

    private async Task VerifyPromotionsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var sourceCount = await _db.Coupons.CountAsync(cancellationToken)
            + await _db.Combos.CountAsync(cancellationToken)
            + await _db.PriceSchedules.CountAsync(cancellationToken);
        var targetCount = await _db.Promotions.CountAsync(cancellationToken);
        report.MutableSourceCounts["promotions"] = sourceCount;
        report.MutableTargetCounts["promotions"] = targetCount;
        CompareCounts(report, "Promotions", sourceCount, targetCount);
        var promotions = await _db.Promotions.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var promotion in promotions)
        {
            VerifyJson(report, "Promotion", $"Promotion:{promotion.Type}:{promotion.Code}", promotion.PayloadJson);
        }

        await VerifyPromotionKeyAsync(report, "coupon", await _db.Coupons.AsNoTracking().Select(coupon => coupon.Id).ToListAsync(cancellationToken), cancellationToken);
        await VerifyPromotionKeyAsync(report, "combo", await _db.Combos.AsNoTracking().Select(combo => combo.Id).ToListAsync(cancellationToken), cancellationToken);
        await VerifyPromotionKeyAsync(report, "price-schedule", await _db.PriceSchedules.AsNoTracking().Select(schedule => schedule.Id).ToListAsync(cancellationToken), cancellationToken);
    }

    private async Task VerifyReviewsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var reviews = await _db.Reviews.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["reviews"] = reviews.Count;
        var targetCount = 0;
        foreach (var review in reviews)
        {
            var sourceId = SourceId("Review", review.Id);
            if (VerifyDocument<ReviewMetadataDocument>(report, "ReviewMetadata", sourceId, review.MetadataJson, _serializer.TryDeserialize<ReviewMetadataDocument>))
            {
                targetCount++;
                var document = _serializer.Deserialize<ReviewMetadataDocument>(review.MetadataJson);
                if (document.HelpfulCount != review.HelpfulCount || document.ReportCount != review.ReportCount)
                    report.AddError("Review", sourceId, $"Review count mismatch: source helpful/report {review.HelpfulCount}/{review.ReportCount}, target {document.HelpfulCount}/{document.ReportCount}.");
            }
            var helpfulCount = await _db.ReviewHelpfuls.CountAsync(vote => vote.ReviewId == review.Id, cancellationToken);
            var reportCount = await _db.ReviewReports.CountAsync(item => item.ReviewId == review.Id, cancellationToken);
            if (review.HelpfulCount != helpfulCount || review.ReportCount != reportCount)
                report.AddError("Review", SourceId("Review", review.Id), $"Review count mismatch: metadata helpful/report {review.HelpfulCount}/{review.ReportCount}, source interactions {helpfulCount}/{reportCount}.");
        }
        report.MutableTargetCounts["reviews"] = targetCount;
        CompareCounts(report, "Reviews", reviews.Count, targetCount);
    }

    private async Task VerifyReturnsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var requests = await _db.ReturnRequests.AsNoTracking().ToListAsync(cancellationToken);
        var targets = await _db.Returns.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["returns"] = requests.Count;
        report.MutableTargetCounts["returns"] = targets.Count;
        CompareCounts(report, "Returns", requests.Count, targets.Count);
        foreach (var request in requests)
        {
            var target = targets.SingleOrDefault(item => item.OrderId == request.OrderId);
            var sourceId = $"Return:{request.OrderId}";
            if (target is null)
            {
                report.AddError("Return", sourceId, "Return target is missing.");
                continue;
            }
            if (target.ApprovedAmount != request.ApprovedAmount)
                report.AddError("Return", sourceId, $"Approved return amount mismatch: source {request.ApprovedAmount}, target {target.ApprovedAmount}.");
            if (!VerifyDocument<ReturnDetailsDocument>(report, "Return", sourceId, target.DetailsJson, _serializer.TryDeserialize<ReturnDetailsDocument>))
                continue;
            var details = _serializer.Deserialize<ReturnDetailsDocument>(target.DetailsJson);
            if (details.ApprovedAmount != request.ApprovedAmount)
                report.AddError("Return", sourceId, $"Approved return amount in details mismatch: source {request.ApprovedAmount}, target {details.ApprovedAmount}.");
            var sourceItemCount = await _db.ReturnRequestItems.CountAsync(item => item.ReturnRequestId == request.Id, cancellationToken);
            var sourceEvidenceCount = await _db.ReturnEvidence.CountAsync(item => item.ReturnRequestId == request.Id, cancellationToken);
            var sourceEventCount = await _db.ReturnEvents.CountAsync(item => item.ReturnRequestId == request.Id, cancellationToken);
            if (details.Items.Count != sourceItemCount || details.Evidence.Count != sourceEvidenceCount || details.Events.Count != sourceEventCount)
                report.AddError("Return", sourceId, $"Return detail count mismatch: source {sourceItemCount}/{sourceEvidenceCount}/{sourceEventCount}, target {details.Items.Count}/{details.Evidence.Count}/{details.Events.Count}.");
        }
    }

    private async Task VerifyContentEntriesAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var sourceCount = await _db.Faqs.CountAsync(cancellationToken);
        var entries = await _db.ContentEntries.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["content"] = sourceCount;
        report.MutableTargetCounts["content"] = entries.Count;
        CompareCounts(report, "ContentEntries", sourceCount, entries.Count);
        foreach (var entry in entries)
            VerifyDocument<ContentPayload>(report, "ContentEntry", $"ContentEntry:{entry.EntryType}:{entry.Key}", entry.PayloadJson, _serializer.TryDeserialize<ContentPayload>);
        var faqs = await _db.Faqs.AsNoTracking().Select(faq => faq.Id).ToListAsync(cancellationToken);
        var entryKeys = entries.Select(entry => (entry.EntryType, entry.Key)).ToHashSet();
        foreach (var faqId in faqs)
        {
            var key = ContentKey("faq", faqId);
            if (!entryKeys.Contains(("faq", key)))
                report.AddError("ContentEntry", SourceId("Faq", faqId), "Content target is missing.");
        }
    }

    private async Task VerifyChatSessionsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var sessions = await _db.ChatSessions.AsNoTracking().ToListAsync(cancellationToken);
        report.MutableSourceCounts["chatSessions"] = sessions.Count;
        var targetCount = 0;
        foreach (var session in sessions)
        {
            if (VerifyDocument<ChatMessagesDocument>(report, "ChatMessages", SourceId("ChatSession", session.Id), session.MessagesJson, _serializer.TryDeserialize<ChatMessagesDocument>))
                targetCount++;
        }
        report.MutableTargetCounts["chatSessions"] = targetCount;
        CompareCounts(report, "ChatSessions", sessions.Count, targetCount);
    }

    private async Task VerifyAuditLogsAsync(ConsolidationVerificationReport report, CancellationToken cancellationToken)
    {
        var sourceCount = await _db.ProductLogs.CountAsync(cancellationToken)
            + await _db.ComboAuditLogs.CountAsync(cancellationToken)
            + await _db.UserAccountLogs.CountAsync(cancellationToken)
            + await _db.RbacAuditLogs.CountAsync(cancellationToken);
        var targetCount = await _db.AuditLogs.CountAsync(cancellationToken);
        report.MutableSourceCounts["auditLogs"] = sourceCount;
        report.MutableTargetCounts["auditLogs"] = targetCount;
        CompareCounts(report, "AuditLogs", sourceCount, targetCount);
        var logs = await _db.AuditLogs.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var log in logs)
        {
            VerifyOptionalJson(report, "AuditLog", $"AuditLog:{log.EntityType}:{log.EntityId}", log.OldValue);
            VerifyOptionalJson(report, "AuditLog", $"AuditLog:{log.EntityType}:{log.EntityId}", log.NewValue);
        }

        var productLogIds = await _db.ProductLogs.AsNoTracking().Select(log => log.Id).ToListAsync(cancellationToken);
        var comboLogIds = await _db.ComboAuditLogs.AsNoTracking().Select(log => checked((int)log.Id)).ToListAsync(cancellationToken);
        var accountLogIds = await _db.UserAccountLogs.AsNoTracking().Select(log => log.Id).ToListAsync(cancellationToken);
        var rbacLogIds = await _db.RbacAuditLogs.AsNoTracking().Select(log => log.Id).ToListAsync(cancellationToken);
        VerifyAuditKeys(report, "ProductLog", productLogIds, logs);
        VerifyAuditKeys(report, "ComboAuditLog", comboLogIds, logs);
        VerifyAuditKeys(report, "UserAccountLog", accountLogIds, logs);
        VerifyAuditKeys(report, "RbacAuditLog", rbacLogIds, logs);
    }

    private bool VerifyDocument<T>(
        ConsolidationVerificationReport report,
        string aggregateType,
        string sourceId,
        string json,
        TryDeserialize<T> tryDeserialize)
    {
        if (!VerifyJson(report, aggregateType, sourceId, json))
            return false;
        if (tryDeserialize(json, out _, out var error))
            return true;
        report.AddError(aggregateType, sourceId, $"Typed JSON validation failed: {error}");
        return false;
    }

    private bool VerifyJson(ConsolidationVerificationReport report, string aggregateType, string sourceId, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            report.IsJsonValid = false;
            report.AddError(aggregateType, sourceId, "ISJSON failed: JSON is empty.");
            return false;
        }
        try
        {
            if (_db.Database.IsSqlServer())
            {
                // SQL Server's check constraint uses ISJSON. Parsing here keeps verification
                // provider-neutral while applying the same validity rule to SQLite tests.
                using var parsed = JsonDocument.Parse(json);
            }
            else
            {
                using var parsed = JsonDocument.Parse(json);
            }
            return true;
        }
        catch (JsonException exception)
        {
            report.IsJsonValid = false;
            report.AddError(aggregateType, sourceId, $"ISJSON failed: {exception.Message}", exception.GetType().Name);
            return false;
        }
    }

    private void VerifyOptionalJson(ConsolidationVerificationReport report, string aggregateType, string sourceId, string? json)
    {
        if (json is not null)
            VerifyJson(report, aggregateType, sourceId, json);
    }

    private bool VerifyWishlistJson(ConsolidationVerificationReport report, User user)
    {
        if (!VerifyJson(report, "UserWishlist", SourceId("User", user.Id), user.WishlistJson))
            return false;
        try
        {
            using var document = JsonDocument.Parse(user.WishlistJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Number))
            {
                throw new JsonException("Wishlist JSON must be an array of product IDs.");
            }
            return true;
        }
        catch (JsonException exception)
        {
            report.IsJsonValid = false;
            report.AddError("UserWishlist", SourceId("User", user.Id), $"Typed wishlist validation failed: {exception.Message}");
            return false;
        }
    }

    private async Task VerifyPromotionKeyAsync(
        ConsolidationVerificationReport report,
        string type,
        IReadOnlyCollection<int> sourceIds,
        CancellationToken cancellationToken)
    {
        var targetKeys = await _db.Promotions
            .AsNoTracking()
            .Where(promotion => promotion.Type == type)
            .Select(promotion => promotion.Code)
            .ToListAsync(cancellationToken);
        foreach (var sourceId in sourceIds)
        {
            var key = PromotionKey(type, sourceId);
            if (!targetKeys.Contains(key, StringComparer.Ordinal))
                report.AddError("Promotion", SourceId(type, sourceId), $"Promotion target key {key} is missing.");
        }
    }

    private static void VerifyAuditKeys(
        ConsolidationVerificationReport report,
        string entityType,
        IReadOnlyCollection<int> sourceIds,
        IReadOnlyCollection<AuditLog> logs)
    {
        foreach (var sourceId in sourceIds)
        {
            if (!logs.Any(log => log.EntityType == entityType && log.EntityId == sourceId))
                report.AddError("AuditLog", SourceId(entityType, sourceId), "Audit target is missing.");
        }
    }

    private static void CompareCounts(ConsolidationVerificationReport report, string aggregateType, int sourceCount, int targetCount)
    {
        if (sourceCount != targetCount)
            report.AddError(aggregateType, $"count:{aggregateType}", $"Count mismatch: source {sourceCount}, target {targetCount}.");
    }

    private async Task<bool> ProductExistsAsync(int productId, CancellationToken cancellationToken) =>
        await _db.Products.AsNoTracking().AnyAsync(product => product.Id == productId, cancellationToken);

    private async Task<bool> ComboExistsAsync(int comboId, CancellationToken cancellationToken) =>
        await _db.Combos.AsNoTracking().AnyAsync(combo => combo.Id == comboId, cancellationToken);

    private async Task<bool> ReturnItemExistsAsync(int itemId, int requestId, CancellationToken cancellationToken) =>
        await _db.ReturnRequestItems.AsNoTracking().AnyAsync(item => item.Id == itemId && item.ReturnRequestId == requestId, cancellationToken);

    private async Task RequireProductReferenceAsync(int productId, int? variantId, string sourceId, CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(productId, cancellationToken))
            throw new InvalidOperationException($"{sourceId} references missing product {productId}.");
        if (!variantId.HasValue)
            return;
        var validVariant = await _db.ProductVariants.AsNoTracking().AnyAsync(
            variant => variant.Id == variantId.Value && variant.ProductId == productId,
            cancellationToken);
        if (!validVariant)
            throw new InvalidOperationException($"{sourceId} references invalid product variant {variantId.Value}.");
    }

    private async Task RequireUserReferenceAsync(int userId, string sourceId, CancellationToken cancellationToken)
    {
        if (!await _db.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            throw new InvalidOperationException($"{sourceId} references missing user {userId}.");
    }

    private async Task RequireOrderReferenceAsync(int orderId, string sourceId, CancellationToken cancellationToken)
    {
        if (!await _db.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId, cancellationToken))
            throw new InvalidOperationException($"{sourceId} references missing order {orderId}.");
    }

    private static string RequireText(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{field} is required.")
            : value;

    private static DateTime RequireDate(DateTime value, string field) =>
        value == default ? throw new InvalidOperationException($"{field} has no timestamp.") : value;

    private static string SourceId(string type, object id) => $"{type}:{id}";

    private static string PromotionKey(string type, int id) => $"{type}:{id}";

    private static string ContentKey(string type, int id) => $"{type}:{id}";

    private static DateTime LegacyPromotionTimestamp(int sourceId) =>
        new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(sourceId);

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;

    private static ChatMessageMetadata? ParseChatMetadata(string? json, long messageId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException($"ChatMessage:{messageId}.MetaJson must be an object.");
        bool? refused = null;
        string? action = null;
        if (TryGetProperty(document.RootElement, "refused", out var refusedValue))
        {
            if (refusedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
                throw new JsonException($"ChatMessage:{messageId}.MetaJson.refused must be a boolean.");
            if (refusedValue.ValueKind != JsonValueKind.Null)
                refused = refusedValue.GetBoolean();
        }
        if (TryGetProperty(document.RootElement, "action", out var actionValue))
        {
            if (actionValue.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                throw new JsonException($"ChatMessage:{messageId}.MetaJson.action must be a string.");
            action = actionValue.GetString();
        }
        return new ChatMessageMetadata { Refused = refused, Action = action };
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? ToAuditJson(string? details)
    {
        if (details is null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(details);
            return details;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { details }, WishlistJsonOptions);
        }
    }

    private static void ValidateOptionalJson(string? json, string field)
    {
        if (json is null)
            return;
        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new JsonException($"{field} is invalid JSON.", exception);
        }
    }

    private static bool SamePayment(Payment left, Payment right) =>
        left.OrderId == right.OrderId
        && left.Provider == right.Provider
        && left.ProviderTransactionId == right.ProviderTransactionId
        && left.Amount == right.Amount
        && left.Status == right.Status
        && left.PaymentCode == right.PaymentCode
        && left.ReferenceCode == right.ReferenceCode
        && left.Message == right.Message
        && left.CreatedAtUtc == right.CreatedAtUtc
        && left.PaidAtUtc == right.PaidAtUtc
        && left.UpdatedAtUtc == right.UpdatedAtUtc;

    private static void CopyPayment(Payment target, Payment source)
    {
        target.OrderId = source.OrderId;
        target.Amount = source.Amount;
        target.Status = source.Status;
        target.PaymentCode = source.PaymentCode;
        target.ReferenceCode = source.ReferenceCode;
        target.Message = source.Message;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.PaidAtUtc = source.PaidAtUtc;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
    }

    private static bool SamePromotion(Promotion left, Promotion right) =>
        left.Type == right.Type
        && left.Code == right.Code
        && left.PayloadJson == right.PayloadJson
        && left.IsActive == right.IsActive
        && left.StartsAt == right.StartsAt
        && left.EndsAt == right.EndsAt
        && left.Revision == right.Revision
        && left.CreatedAt == right.CreatedAt
        && left.UpdatedAt == right.UpdatedAt;

    private static void CopyPromotion(Promotion target, Promotion source)
    {
        target.PayloadJson = source.PayloadJson;
        target.IsActive = source.IsActive;
        target.StartsAt = source.StartsAt;
        target.EndsAt = source.EndsAt;
        target.Revision = source.Revision;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }

    private static bool SameReturn(ReturnCase left, ReturnCase right) =>
        left.ReturnNumber == right.ReturnNumber
        && left.OrderId == right.OrderId
        && left.UserId == right.UserId
        && left.Status == right.Status
        && left.SubmittedAtUtc == right.SubmittedAtUtc
        && left.ClaimDeadlineAtUtc == right.ClaimDeadlineAtUtc
        && left.SupplementDeadlineAtUtc == right.SupplementDeadlineAtUtc
        && left.SupplementCount == right.SupplementCount
        && left.RequestedAmount == right.RequestedAmount
        && left.ApprovedAmount == right.ApprovedAmount
        && left.ApprovedShippingFeeAmount == right.ApprovedShippingFeeAmount
        && left.DetailsJson == right.DetailsJson;

    private static void CopyReturn(ReturnCase target, ReturnCase source)
    {
        target.ReturnNumber = source.ReturnNumber;
        target.UserId = source.UserId;
        target.Status = source.Status;
        target.SubmittedAtUtc = source.SubmittedAtUtc;
        target.ClaimDeadlineAtUtc = source.ClaimDeadlineAtUtc;
        target.SupplementDeadlineAtUtc = source.SupplementDeadlineAtUtc;
        target.SupplementCount = source.SupplementCount;
        target.RequestedAmount = source.RequestedAmount;
        target.ApprovedAmount = source.ApprovedAmount;
        target.ApprovedShippingFeeAmount = source.ApprovedShippingFeeAmount;
        target.DetailsJson = source.DetailsJson;
    }

    private static bool SameContentEntry(ContentEntry left, ContentEntry right) =>
        left.EntryType == right.EntryType
        && left.Key == right.Key
        && left.Title == right.Title
        && left.PayloadJson == right.PayloadJson
        && left.IsActive == right.IsActive
        && left.IsRead == right.IsRead
        && left.CreatedAt == right.CreatedAt
        && left.UpdatedAt == right.UpdatedAt;

    private static void CopyContentEntry(ContentEntry target, ContentEntry source)
    {
        target.Title = source.Title;
        target.PayloadJson = source.PayloadJson;
        target.IsActive = source.IsActive;
        target.IsRead = source.IsRead;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }

    private static bool SameAuditLog(AuditLog left, AuditLog right) =>
        left.Action == right.Action
        && left.EntityType == right.EntityType
        && left.EntityId == right.EntityId
        && left.ChangedByAdminId == right.ChangedByAdminId
        && left.ChangedAt == right.ChangedAt
        && left.OldValue == right.OldValue
        && left.NewValue == right.NewValue;

    private static void CopyAuditLog(AuditLog target, AuditLog source)
    {
        target.Action = source.Action;
        target.ChangedByAdminId = source.ChangedByAdminId;
        target.ChangedAt = source.ChangedAt;
        target.OldValue = source.OldValue;
        target.NewValue = source.NewValue;
    }

    private delegate bool TryDeserialize<T>(string json, out T? document, out string? error);
}
