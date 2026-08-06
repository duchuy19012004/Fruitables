using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Models.Returns;
using Fruitables.Services.Infrastructure.DatabaseConsolidation;
using Fruitables.Services.Infrastructure.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fruitables.Tests;

public class DatabaseConsolidationBackfillTests
{
    [Fact]
    public async Task Dry_run_plans_each_aggregate_without_writing_target_data()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);

        var before = await DatabaseConsolidationFixture.SnapshotAsync(db);
        var rootJsonBefore = await DatabaseConsolidationFixture.RootJsonSnapshotAsync(db);
        var service = CreateService(db);

        var report = await service.BackfillAsync(apply: false, CancellationToken.None);

        Assert.True(report.Success);
        Assert.False(report.Applied);
        Assert.True(report.Planned > 0);
        Assert.Equal(0, report.Processed);
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.Equal(0, await db.Promotions.CountAsync());
        Assert.Equal(0, await db.ContentEntries.CountAsync());
        Assert.Equal(0, await db.Returns.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
        Assert.Equal(before, await DatabaseConsolidationFixture.SnapshotAsync(db));
        Assert.Equal(rootJsonBefore, await DatabaseConsolidationFixture.RootJsonSnapshotAsync(db));
    }

    [Fact]
    public async Task Apply_is_idempotent_and_preserves_legacy_source_rows()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);

        var before = await DatabaseConsolidationFixture.SnapshotAsync(db);
        var service = CreateService(db);

        var first = await service.BackfillAsync(apply: true, CancellationToken.None);
        var targetCountsAfterFirst = await DatabaseConsolidationFixture.TargetCountsAsync(db);
        var second = await service.BackfillAsync(apply: true, CancellationToken.None);
        var targetCountsAfterSecond = await DatabaseConsolidationFixture.TargetCountsAsync(db);

        Assert.True(first.Success, string.Join(Environment.NewLine, first.Errors.Select(error => error.Message)));
        Assert.True(second.Success, string.Join(Environment.NewLine, second.Errors.Select(error => error.Message)));
        Assert.True(first.Processed > 0);
        Assert.True(second.Skipped > 0);
        Assert.Equal(targetCountsAfterFirst, targetCountsAfterSecond);
        Assert.Equal(1, targetCountsAfterSecond.Payments);
        Assert.Equal(3, targetCountsAfterSecond.Promotions);
        Assert.Equal(7, targetCountsAfterSecond.ContentEntries);
        Assert.Equal(1, targetCountsAfterSecond.Returns);
        Assert.Equal(4, targetCountsAfterSecond.AuditLogs);
        Assert.Equal(before, await DatabaseConsolidationFixture.SnapshotAsync(db));

        var product = await db.Products.SingleAsync(product => product.Id == DatabaseConsolidationFixture.ProductId);
        var user = await db.Users.SingleAsync(user => user.Id == DatabaseConsolidationFixture.UserId);
        var order = await db.Orders.SingleAsync(order => order.Id == DatabaseConsolidationFixture.OrderId);
        var review = await db.Reviews.SingleAsync(review => review.Id == DatabaseConsolidationFixture.ReviewId);
        var chat = await db.ChatSessions.SingleAsync();
        Assert.True(JsonDocument.Parse(product.ImagesJson).RootElement.TryGetProperty("images", out _));
        Assert.True(JsonDocument.Parse(product.TagsJson).RootElement.TryGetProperty("tags", out _));
        Assert.True(JsonDocument.Parse(user.RoleIdsJson).RootElement.TryGetProperty("roles", out _));
        Assert.NotEqual("[]", user.WishlistJson);
        Assert.True(JsonDocument.Parse(order.StatusHistoryJson).RootElement.TryGetProperty("entries", out _));
        Assert.True(JsonDocument.Parse(order.NotesJson).RootElement.TryGetProperty("notes", out _));
        Assert.True(JsonDocument.Parse(review.MetadataJson).RootElement.TryGetProperty("status", out _));
        Assert.True(JsonDocument.Parse(chat.MessagesJson).RootElement.TryGetProperty("messages", out _));
    }

    [Fact]
    public async Task Invalid_json_or_reference_is_reported_without_dropping_source_rows()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            Id = 999,
            Action = "Update",
            EntityType = "Role",
            EntityId = DatabaseConsolidationFixture.RoleId,
            ChangedByAdminId = DatabaseConsolidationFixture.AdminId,
            OldValue = "not-json",
            NewValue = "{}"
        });
        db.SePayTransactions.Add(new SePayTransaction
        {
            Id = 998,
            SePayTransactionId = 998,
            OrderId = null,
            TransferAmount = 42m,
            Status = SePayTransactionStatus.Paid
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.BackfillAsync(apply: true, CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error => error.SourceId == "RbacAuditLog:999");
        Assert.Contains(report.Errors, error => error.SourceId == "SePayTransaction:998");
        Assert.Equal(2, await db.RbacAuditLogs.CountAsync());
        Assert.Equal(2, await db.SePayTransactions.CountAsync());
        Assert.Null(await db.AuditLogs.SingleOrDefaultAsync(log => log.EntityId == 999));
        Assert.Null(await db.Payments.SingleOrDefaultAsync(payment => payment.ProviderTransactionId == "998"));
    }

    [Fact]
    public async Task Invalid_product_and_combo_audit_json_is_reported_without_dropping_source_rows()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        db.ProductLogs.Add(new ProductLog
        {
            Id = 1005,
            ProductId = DatabaseConsolidationFixture.ProductId,
            AdminId = DatabaseConsolidationFixture.AdminId,
            Action = ProductLogActions.Update,
            Details = "not-json"
        });
        db.ComboAuditLogs.Add(new ComboAuditLog
        {
            Id = 1006,
            ComboId = DatabaseConsolidationFixture.ComboId,
            AdminId = DatabaseConsolidationFixture.AdminId,
            Action = ComboAuditActions.Update,
            Revision = 1,
            Details = "not-json"
        });
        await db.SaveChangesAsync();

        var report = await CreateService(db).BackfillAsync(apply: true, CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error => error.SourceId == "ProductLog:1005");
        Assert.Contains(report.Errors, error => error.SourceId == "ComboAuditLog:1006");
        Assert.NotNull(await db.ProductLogs.SingleOrDefaultAsync(log => log.Id == 1005));
        Assert.NotNull(await db.ComboAuditLogs.SingleOrDefaultAsync(log => log.Id == 1006));
        Assert.Null(await db.AuditLogs.SingleOrDefaultAsync(log => log.SourceType == "ProductLog" && log.SourceId == 1005));
        Assert.Null(await db.AuditLogs.SingleOrDefaultAsync(log => log.SourceType == "ComboAuditLog" && log.SourceId == 1006));
    }

    [Fact]
    public async Task Ignored_and_duplicate_sepay_events_are_represented_but_not_business_payments()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        db.SePayTransactions.AddRange(
            new SePayTransaction
            {
                Id = 1007,
                SePayTransactionId = 12346,
                OrderId = DatabaseConsolidationFixture.OrderId,
                TransferAmount = 21m,
                Status = SePayTransactionStatus.Ignored,
                Message = "Amount mismatch"
            },
            new SePayTransaction
            {
                Id = 1008,
                SePayTransactionId = 12347,
                OrderId = DatabaseConsolidationFixture.OrderId,
                TransferAmount = 21m,
                Status = SePayTransactionStatus.Duplicate,
                Message = "Duplicate event"
            });
        await db.SaveChangesAsync();

        var report = await CreateService(db).BackfillAsync(apply: true, CancellationToken.None);

        Assert.True(report.Success, string.Join(Environment.NewLine, report.Errors.Select(error => error.Message)));
        var ignored = await db.Payments.SingleAsync(payment => payment.ProviderTransactionId == "12346");
        var duplicate = await db.Payments.SingleAsync(payment => payment.ProviderTransactionId == "12347");
        Assert.Equal(PaymentProviderEventStatus.Ignored, ignored.ProviderEventStatus);
        Assert.Equal(PaymentProviderEventStatus.Duplicate, duplicate.ProviderEventStatus);
        Assert.False(ignored.IsBusinessPayment);
        Assert.False(duplicate.IsBusinessPayment);
        Assert.Equal(3, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Audit_source_identity_is_unique_and_preserves_multiple_events_for_one_product()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        db.ProductLogs.Add(new ProductLog
        {
            Id = 1009,
            ProductId = DatabaseConsolidationFixture.ProductId,
            AdminId = DatabaseConsolidationFixture.AdminId,
            Action = ProductLogActions.TagUpdate,
            Details = "{\"field\":\"tags\"}"
        });
        await db.SaveChangesAsync();

        var report = await CreateService(db).BackfillAsync(apply: true, CancellationToken.None);

        Assert.True(report.Success, string.Join(Environment.NewLine, report.Errors.Select(error => error.Message)));
        Assert.Equal(2, await db.AuditLogs.CountAsync(log => log.SourceType == "ProductLog"));
        var index = db.Model.FindEntityType(typeof(AuditLog))!
            .GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name).SequenceEqual(["SourceType", "SourceId"]));
        Assert.True(index.IsUnique);

        db.AuditLogs.Add(new AuditLog
        {
            SourceType = "ProductLog",
            SourceId = 1001,
            EntityType = "ProductLog",
            EntityId = 1001,
            Action = ProductLogActions.Update,
            ChangedByAdminId = DatabaseConsolidationFixture.AdminId,
            NewValue = "{}"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Apply_reports_a_transaction_failure_without_partially_updating_product_json()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var before = await db.Products
            .Where(product => product.Id == DatabaseConsolidationFixture.ProductId)
            .Select(product => new { product.ImagesJson, product.TagsJson })
            .SingleAsync();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailProductJsonUpdate BEFORE UPDATE OF ImagesJson ON Products BEGIN SELECT RAISE(ABORT, 'forced target failure'); END;");

        var report = await CreateService(db).BackfillAsync(apply: true, CancellationToken.None);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, error => error.SourceId == "Product:10");
        var after = await db.Products
            .Where(product => product.Id == DatabaseConsolidationFixture.ProductId)
            .Select(product => new { product.ImagesJson, product.TagsJson })
            .SingleAsync();
        Assert.Equal(before.ImagesJson, after.ImagesJson);
        Assert.Equal(before.TagsJson, after.TagsJson);
    }

    [Fact]
    public async Task Rerun_preserves_target_owned_content_read_state_and_return_links()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        await using var db = new ApplicationDbContext(options);
        await DatabaseConsolidationFixture.SeedAsync(db);
        var service = CreateService(db);
        var first = await service.BackfillAsync(apply: true, CancellationToken.None);
        Assert.True(first.Success, string.Join(Environment.NewLine, first.Errors.Select(error => error.Message)));

        var content = await db.ContentEntries.SingleAsync(entry => entry.EntryType == "faq" && entry.Key == "faq:801");
        content.IsRead = true;
        await db.SaveChangesAsync();
        var second = await service.BackfillAsync(apply: true, CancellationToken.None);
        var detailsJson = await db.Returns.Select(item => item.DetailsJson).SingleAsync();
        var details = new VersionedJsonSerializer().Deserialize<ReturnDetailsDocument>(detailsJson);

        Assert.True(second.Success, string.Join(Environment.NewLine, second.Errors.Select(error => error.Message)));
        Assert.True(await db.ContentEntries.Where(entry => entry.EntryType == "faq" && entry.Key == "faq:801").Select(entry => entry.IsRead).SingleAsync());
        Assert.Equal(701, details.Evidence.Single().ReturnRequestItemId);
        Assert.Equal(701, details.Events.Single().ReturnRequestItemId);
    }

    private static IDatabaseConsolidationService CreateService(ApplicationDbContext db) =>
        new DatabaseConsolidationService(
            db,
            new VersionedJsonSerializer(),
            NullLogger<DatabaseConsolidationService>.Instance);
}

internal static class DatabaseConsolidationFixture
{
    public const int AdminId = 1;
    public const int UserId = 10;
    public const int ProductId = 10;
    public const int VariantId = 102;
    public const int RoleId = 20;
    public const int PermissionId = 30;
    public const int ComboId = 40;
    public const int OrderId = 50;
    public const int ReviewId = 70;
    public const int ReturnRequestId = 80;

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var admin = await db.Users.FindAsync(AdminId);
        if (admin is null)
        {
            admin = new User
            {
                Id = AdminId,
                Name = "Fixture Admin",
                Email = "fixture-admin@example.test",
                Password = "not-used",
                Role = UserRole.Admin
            };
            db.Users.Add(admin);
        }

        var category = new Category { Id = 10, Name = "Fixture", Slug = "fixture" };
        var tag = new ProductTag { Id = 101, Name = "Fresh", Slug = "fresh" };
        var product = new Product
        {
            Id = ProductId,
            CategoryId = category.Id,
            Name = "Fixture Apple",
            Slug = "fixture-apple",
            Price = 10m,
            StockQuantity = 25m,
            ReviewCount = 1,
            AverageRating = 5m,
            Images =
            [
                new ProductImage
                {
                    Id = 100,
                    ImageUrl = "/uploads/products/apple.jpg",
                    IsPrimary = true,
                    SortOrder = 0
                }
            ],
            Tags = [tag],
            Variants =
            [
                new ProductVariant
                {
                    Id = VariantId,
                    SKU = "APPLE-1",
                    Name = "One kilo",
                    Price = 10m,
                    StockQuantity = 12m
                }
            ]
        };
        db.Categories.Add(category);
        db.Products.Add(product);

        var permission = new Permission
        {
            Id = PermissionId,
            Name = "fixture.read",
            Module = "fixture"
        };
        var role = new Role { Id = RoleId, Name = "FixtureManager" };
        var user = new User
        {
            Id = UserId,
            Name = "Fixture Customer",
            Email = "fixture-customer@example.test",
            Password = "not-used",
            Role = UserRole.Customer
        };
        db.Permissions.Add(permission);
        db.Roles.Add(role);
        db.Users.Add(user);
        db.RolePermissions.Add(new RolePermission
        {
            Id = 301,
            RoleId = RoleId,
            PermissionId = PermissionId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = AdminId
        });
        db.UserRoleMappings.Add(new UserRoleMapping
        {
            Id = 302,
            UserId = UserId,
            RoleId = RoleId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAdminId = AdminId
        });
        db.Wishlists.Add(new Wishlist
        {
            Id = 303,
            UserId = UserId,
            ProductId = ProductId
        });

        var combo = new Combo
        {
            Id = ComboId,
            Name = "Fixture bundle",
            Slug = "fixture-bundle",
            PricingType = ComboPricingType.FixedPrice,
            FixedPrice = 9m,
            Items =
            [
                new ComboItem
                {
                    Id = 401,
                    ProductId = ProductId,
                    ProductVariantId = VariantId,
                    Quantity = 1m,
                    SortOrder = 0
                }
            ]
        };
        db.Combos.Add(combo);
        db.Coupons.Add(new Coupon
        {
            Id = 402,
            Code = "FIXTURE10",
            Type = CouponType.Percentage,
            Value = 10m,
            MinQuantity = 1m
        });
        db.PriceSchedules.Add(new PriceSchedule
        {
            Id = 403,
            ProductId = ProductId,
            ProductVariantId = VariantId,
            DiscountType = DiscountType.FixedPrice,
            Value = 9m,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        db.Carts.Add(new Cart
        {
            Id = 404,
            UserId = UserId,
            Items =
            [
                new CartItem
                {
                    Id = 405,
                    ProductId = ProductId,
                    ProductVariantId = VariantId,
                    Quantity = 2m,
                    Price = 10m,
                    CartGroupId = 406
                }
            ],
            Groups =
            [
                new CartGroup
                {
                    Id = 406,
                    ComboId = ComboId,
                    ComboRevision = 1,
                    ComboName = "Fixture bundle",
                    Quantity = 1,
                    OriginalTotal = 10m,
                    FinalTotal = 9m,
                    Discount = 1m
                }
            ]
        });

        var order = new Order
        {
            Id = OrderId,
            UserId = UserId,
            OrderNumber = "FIXTURE-ORDER",
            Status = OrderStatus.Delivered,
            Subtotal = 20m,
            ShippingFee = 2m,
            Discount = 1m,
            Total = 21m,
            Items =
            [
                new OrderItem
                {
                    Id = 501,
                    ProductId = ProductId,
                    ProductVariantId = VariantId,
                    ProductName = "Fixture Apple",
                    Quantity = 2m,
                    Price = 10m,
                    Total = 20m
                }
            ],
            StatusHistory =
            [
                new OrderStatusHistory
                {
                    Id = 502,
                    OldStatus = OrderStatus.Processing,
                    NewStatus = OrderStatus.Delivered,
                    AdminId = AdminId,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            OrderNotes =
            [
                new OrderNote
                {
                    Id = 503,
                    AdminId = AdminId,
                    AdminName = "Fixture Admin",
                    Content = "Fixture note",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        db.Orders.Add(order);
        db.SePayTransactions.Add(new SePayTransaction
        {
            Id = 504,
            SePayTransactionId = 12345,
            OrderId = OrderId,
            PaymentCode = "FIXTURE",
            TransferAmount = 21m,
            ReferenceCode = "REF-1",
            Status = SePayTransactionStatus.Paid,
            Message = "Paid"
        });

        db.Reviews.Add(new Review
        {
            Id = ReviewId,
            ProductId = ProductId,
            UserId = UserId,
            Rating = 5,
            Comment = "Fresh",
            Status = ReviewStatus.Approved,
            IsVerifiedPurchase = true,
            HelpfulCount = 1,
            ReportCount = 1,
            CreatedAt = DateTime.UtcNow,
            HelpfulVotes = [new ReviewHelpful { Id = 601, UserId = AdminId }],
            Reports = [new ReviewReport { Id = 602, ReportedByUserId = AdminId }],
            Sentiment = new ReviewSentiment
            {
                Id = 603,
                RatingSentiment = SentimentLabel.Positive,
                Sentiment = SentimentLabel.Positive,
                Source = SentimentSource.RatingFallback,
                Aspects =
                [
                    new ReviewSentimentAspect
                    {
                        Id = 604,
                        Aspect = SentimentAspect.Quality,
                        Sentiment = SentimentLabel.Positive
                    }
                ]
            }
        });

        db.ReturnRequests.Add(new ReturnRequest
        {
            Id = ReturnRequestId,
            ReturnNumber = "FIXTURE-RETURN",
            OrderId = OrderId,
            UserId = UserId,
            Status = ReturnRequestStatus.Refunded,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
            ClaimDeadlineAtUtc = DateTime.UtcNow.AddDays(10),
            RequestedAmount = 20m,
            ApprovedAmount = 10m,
            ApprovedShippingFeeAmount = 2m,
            CustomerNote = "Damaged",
            AdminNote = "Approved",
            Items =
            [
                new ReturnRequestItem
                {
                    Id = 701,
                    OrderItemId = 501,
                    DecisionStatus = ReturnItemDecisionStatus.Approved,
                    RequestedQuantity = 1m,
                    ApprovedQuantity = 1m,
                    Reason = ReturnReasonCode.Damaged,
                    Description = "Damaged fruit",
                    RequestedAmount = 10m,
                    ApprovedAmount = 10m
                }
            ],
            Evidence =
            [
                new ReturnEvidence
                {
                    Id = 702,
                    ReturnRequestItemId = 701,
                    StorageKey = "returns/fixture.jpg",
                    OriginalFileName = "fixture.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 10,
                    UploadedByUserId = UserId
                }
            ],
            Events =
            [
                new ReturnEvent
                {
                    Id = 703,
                    ReturnRequestItemId = 701,
                    EventType = ReturnEventType.Approved,
                    ActorUserId = AdminId,
                    NewStatus = ReturnRequestStatus.Refunded
                }
            ],
            Refund = new Refund
            {
                Id = 704,
                OrderId = OrderId,
                Amount = 10m,
                ShippingFeeAmount = 2m,
                Status = RefundStatus.Succeeded,
                CreatedByUserId = AdminId
            }
        });

        db.Faqs.Add(new Faq
        {
            Id = 801,
            Title = "Fixture FAQ",
            Body = "Fixture answer",
            Category = "fixture"
        });

        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        db.ChatSessions.Add(new ChatSession
        {
            Id = sessionId,
            UserId = UserId,
            Messages =
            [
                new ChatMessage
                {
                    Id = 901,
                    Role = "user",
                    Content = "Hello",
                    MetaJson = "{\"action\":\"greet\",\"refused\":false}"
                },
                new ChatMessage
                {
                    Id = 902,
                    Role = "assistant",
                    Content = "Hi"
                }
            ]
        });

        db.ProductLogs.Add(new ProductLog
        {
            Id = 1001,
            ProductId = ProductId,
            AdminId = AdminId,
            Action = ProductLogActions.Update,
            Details = "{\"field\":\"price\"}"
        });
        db.ComboAuditLogs.Add(new ComboAuditLog
        {
            Id = 1002,
            ComboId = ComboId,
            AdminId = AdminId,
            Action = ComboAuditActions.Update,
            Revision = 1,
            Details = "{\"field\":\"name\"}"
        });
        db.UserAccountLogs.Add(new UserAccountLog
        {
            Id = 1003,
            UserId = UserId,
            AdminId = AdminId,
            Action = "Lock",
            Reason = "Fixture"
        });
        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            Id = 1004,
            Action = "Assign",
            EntityType = "RolePermission",
            EntityId = RoleId,
            ChangedByAdminId = AdminId,
            OldValue = "{}",
            NewValue = "{\"permissionId\":30}"
        });

        await db.SaveChangesAsync();
    }

    public static async Task<SourceSnapshot> SnapshotAsync(ApplicationDbContext db)
    {
        return new SourceSnapshot(
            await db.ProductImages.CountAsync(),
            await db.ProductTags.CountAsync(),
            await db.Wishlists.CountAsync(),
            await db.CartItems.CountAsync(),
            await db.OrderStatusHistories.CountAsync(),
            await db.OrderNotes.CountAsync(),
            await db.SePayTransactions.CountAsync(),
            await db.Coupons.CountAsync(),
            await db.Combos.CountAsync(),
            await db.PriceSchedules.CountAsync(),
            await db.Reviews.CountAsync(),
            await db.ReviewHelpfuls.CountAsync(),
            await db.ReviewReports.CountAsync(),
            await db.ReviewSentiments.CountAsync(),
            await db.ReturnRequests.CountAsync(),
            await db.ReturnRequestItems.CountAsync(),
            await db.ReturnEvidence.CountAsync(),
            await db.ReturnEvents.CountAsync(),
            await db.Refunds.CountAsync(),
            await db.Faqs.CountAsync(),
            await db.ChatMessages.CountAsync(),
            await db.ProductLogs.CountAsync(),
            await db.ComboAuditLogs.CountAsync(),
            await db.UserAccountLogs.CountAsync(),
            await db.RbacAuditLogs.CountAsync());
    }

    public static async Task<RootJsonSnapshot> RootJsonSnapshotAsync(ApplicationDbContext db)
    {
        var product = await db.Products.AsNoTracking().SingleAsync(product => product.Id == ProductId);
        var user = await db.Users.AsNoTracking().SingleAsync(user => user.Id == UserId);
        var cart = await db.Carts.AsNoTracking().SingleAsync(cart => cart.Id == 404);
        var order = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == OrderId);
        var review = await db.Reviews.AsNoTracking().SingleAsync(review => review.Id == ReviewId);
        var chat = await db.ChatSessions.AsNoTracking().SingleAsync();
        return new RootJsonSnapshot(
            product.ImagesJson,
            product.TagsJson,
            user.RoleIdsJson,
            user.WishlistJson,
            cart.LinesJson,
            order.StatusHistoryJson,
            order.NotesJson,
            review.MetadataJson,
            chat.MessagesJson);
    }

    public static async Task<TargetCounts> TargetCountsAsync(ApplicationDbContext db) =>
        new(
            await db.Payments.CountAsync(),
            await db.Promotions.CountAsync(),
            await db.ContentEntries.CountAsync(),
            await db.Returns.CountAsync(),
            await db.AuditLogs.CountAsync());

    public readonly record struct SourceSnapshot(
        int ProductImages,
        int ProductTags,
        int Wishlists,
        int CartItems,
        int OrderStatusHistories,
        int OrderNotes,
        int SePayTransactions,
        int Coupons,
        int Combos,
        int PriceSchedules,
        int Reviews,
        int ReviewHelpfuls,
        int ReviewReports,
        int ReviewSentiments,
        int ReturnRequests,
        int ReturnRequestItems,
        int ReturnEvidence,
        int ReturnEvents,
        int Refunds,
        int Faqs,
        int ChatMessages,
        int ProductLogs,
        int ComboAuditLogs,
        int UserAccountLogs,
        int RbacAuditLogs);

    public readonly record struct RootJsonSnapshot(
        string ProductImages,
        string ProductTags,
        string UserRoles,
        string Wishlist,
        string CartLines,
        string OrderHistory,
        string OrderNotes,
        string ReviewMetadata,
        string ChatMessages);

    public readonly record struct TargetCounts(
        int Payments,
        int Promotions,
        int ContentEntries,
        int Returns,
        int AuditLogs);
}
