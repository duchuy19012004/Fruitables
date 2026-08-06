using System.Text.Json;
using System.Text.Json.Nodes;
using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Models.Returns;
using Fruitables.Services.Infrastructure.Json;
using Xunit;

namespace Fruitables.Tests;

public sealed class JsonDocumentContractTests
{
    private readonly IJsonDocumentSerializer _serializer = new VersionedJsonSerializer();

    [Fact]
    public void ProductImagesDocument_preserves_primary_sort_order_url_and_storage_key()
    {
        var document = new ProductImagesDocument
        {
            Images =
            [
                new ProductImageDocument
                {
                    Url = "https://cdn.example.test/products/apple.webp",
                    StorageKey = "products/42/apple.webp",
                    IsPrimary = true,
                    SortOrder = 3
                }
            ]
        };

        AssertContract(document, "images");

        var roundTrip = _serializer.Deserialize<ProductImagesDocument>(_serializer.Serialize(document));
        var image = Assert.Single(roundTrip.Images);
        Assert.Equal("https://cdn.example.test/products/apple.webp", image.Url);
        Assert.Equal("products/42/apple.webp", image.StorageKey);
        Assert.True(image.IsPrimary);
        Assert.Equal(3, image.SortOrder);
    }

    [Fact]
    public void ProductTagsDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ProductTagsDocument
            {
                Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
            },
            "tags");
    }

    [Fact]
    public void CartLinesDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new CartLinesDocument
            {
                Lines =
                [
                    new CartLineDocument
                    {
                        ProductId = 7,
                        ProductVariantId = 3,
                        CartGroupId = null,
                        Quantity = 2,
                        Price = 12.50m,
                        ComboDiscount = 1.25m
                    }
                ]
            },
            "lines");
    }

    [Fact]
    public void OrderStatusHistoryDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new OrderStatusHistoryDocument
            {
                Entries =
                [
                    new OrderStatusHistoryEntry
                    {
                        OldStatus = OrderStatus.Pending,
                        NewStatus = OrderStatus.Processing,
                        AdminId = 9,
                        Notes = "Packed",
                        CreatedAt = DateTime.Parse("2026-08-07T08:30:00Z").ToUniversalTime()
                    }
                ]
            },
            "entries");
    }

    [Fact]
    public void OrderNotesDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new OrderNotesDocument
            {
                Notes =
                [
                    new OrderNoteDocument
                    {
                        AdminId = 9,
                        AdminName = "Admin",
                        Content = "Customer called",
                        CreatedAt = DateTime.Parse("2026-08-07T08:35:00Z").ToUniversalTime()
                    }
                ]
            },
            "notes");
    }

    [Fact]
    public void CouponPayload_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new CouponPayload
            {
                Code = "FRESH10",
                Type = CouponType.Percentage,
                Value = 10m,
                MinOrderAmount = 100m,
                MinQuantity = 1m,
                MaxUses = 100,
                UsedCount = 2,
                StartDate = DateTime.Parse("2026-08-01T00:00:00Z").ToUniversalTime(),
                EndDate = DateTime.Parse("2026-08-31T23:59:59Z").ToUniversalTime(),
                IsActive = true
            },
            "code");
    }

    [Fact]
    public void ComboPayload_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ComboPayload
            {
                Name = "Breakfast Box",
                Slug = "breakfast-box",
                Description = "Fruit and juice",
                ImageUrl = "https://cdn.example.test/combos/breakfast.webp",
                IsActive = true,
                Status = ComboLifecycleStatus.Active,
                StartsAt = DateTimeOffset.Parse("2026-08-07T00:00:00Z"),
                EndsAt = DateTimeOffset.Parse("2026-08-31T00:00:00Z"),
                PricingType = ComboPricingType.FixedPrice,
                FixedPrice = 99m,
                DiscountValue = null,
                AllowCouponStacking = true,
                Revision = 2,
                SortOrder = 1,
                Items =
                [
                    new ComboItemPayload
                    {
                        ProductId = 7,
                        ProductVariantId = 3,
                        Quantity = 2,
                        SortOrder = 0
                    }
                ]
            },
            "name");
    }

    [Fact]
    public void PriceSchedulePayload_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new PriceSchedulePayload
            {
                ProductId = 7,
                ProductVariantId = 3,
                DiscountType = DiscountType.Percentage,
                Value = 15m,
                StartsAt = DateTimeOffset.Parse("2026-08-07T00:00:00Z"),
                EndsAt = DateTimeOffset.Parse("2026-08-14T00:00:00Z"),
                IsCancelled = false,
                CancelledAt = null,
                CancelledByAdminId = null,
                CancellationReason = null,
                Revision = 2,
                CreatedByAdminId = 9,
                CreatedAt = DateTimeOffset.Parse("2026-08-06T00:00:00Z"),
                UpdatedAt = DateTimeOffset.Parse("2026-08-06T12:00:00Z")
            },
            "productId");
    }

    [Fact]
    public void ReviewMetadataDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ReviewMetadataDocument
            {
                Status = ReviewStatus.Approved,
                IsHidden = false,
                HiddenReason = null,
                HiddenByAdminId = null,
                HiddenAt = null,
                IsDeleted = false,
                DeletedByAdminId = null,
                DeletedAt = null,
                IsVerifiedPurchase = true,
                HelpfulCount = 4,
                ReportCount = 0,
                CreatedAt = DateTime.Parse("2026-08-07T07:00:00Z").ToUniversalTime(),
                UpdatedAt = null
            },
            "status");
    }

    [Fact]
    public void ReturnDetailsDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ReturnDetailsDocument
            {
                Status = ReturnRequestStatus.Submitted,
                SubmittedAtUtc = DateTime.Parse("2026-08-07T07:00:00Z").ToUniversalTime(),
                ClaimDeadlineAtUtc = DateTime.Parse("2026-08-14T07:00:00Z").ToUniversalTime(),
                SupplementDeadlineAtUtc = null,
                SupplementCount = 0,
                RequestedAmount = 25m,
                ApprovedAmount = 0m,
                ApprovedShippingFeeAmount = 0m,
                CustomerNote = "Damaged fruit",
                AdminNote = null,
                Items =
                [
                    new ReturnItemDetails
                    {
                        OrderItemId = 11,
                        DecisionStatus = ReturnItemDecisionStatus.Pending,
                        RequestedQuantity = 1,
                        ApprovedQuantity = 0,
                        Reason = ReturnReasonCode.Damaged,
                        Description = "Bruised",
                        DecisionReason = null,
                        RequestedAmount = 25m,
                        ApprovedAmount = 0m
                    }
                ],
                Evidence =
                [
                    new ReturnEvidenceDetails
                    {
                        StorageKey = "returns/1/photo.webp",
                        OriginalFileName = "photo.webp",
                        ContentType = "image/webp",
                        SizeBytes = 1024,
                        UploadedByUserId = 20,
                        UploadedAtUtc = DateTime.Parse("2026-08-07T07:01:00Z").ToUniversalTime()
                    }
                ],
                Events =
                [
                    new ReturnEventDetails
                    {
                        OldStatus = null,
                        NewStatus = ReturnRequestStatus.Submitted,
                        EventType = ReturnEventType.Submitted,
                        ActorUserId = 20,
                        Note = null,
                        CreatedAtUtc = DateTime.Parse("2026-08-07T07:00:00Z").ToUniversalTime()
                    }
                ],
                Refund = null
            },
            "status");
    }

    [Fact]
    public void ContentPayload_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ContentPayload
            {
                Title = "Shipping FAQ",
                Body = "Orders ship every weekday.",
                Category = "shipping",
                IsActive = true,
                CreatedAt = DateTime.Parse("2026-08-07T06:00:00Z").ToUniversalTime(),
                UpdatedAt = DateTime.Parse("2026-08-07T06:30:00Z").ToUniversalTime()
            },
            "title");
    }

    [Fact]
    public void ChatMessagesDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new ChatMessagesDocument
            {
                Messages =
                [
                    new ChatMessageDocument
                    {
                        Role = "user",
                        Content = "Where is my order?",
                        CreatedAt = DateTime.Parse("2026-08-07T08:00:00Z").ToUniversalTime(),
                        Metadata = new ChatMessageMetadata { Refused = false, Action = "order_status" }
                    }
                ]
            },
            "messages");
    }

    [Fact]
    public void RolePermissionsDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new RolePermissionsDocument
            {
                RoleId = 4,
                Permissions =
                [
                    new RolePermissionEntry
                    {
                        PermissionId = 11,
                        PermissionName = "products.read",
                        AssignedAt = DateTime.Parse("2026-08-07T05:00:00Z").ToUniversalTime(),
                        AssignedByAdminId = 1
                    }
                ]
            },
            "permissions");
    }

    [Fact]
    public void UserRolesDocument_has_a_versioned_round_trip_contract()
    {
        AssertContract(
            new UserRolesDocument
            {
                UserId = 20,
                Roles =
                [
                    new UserRoleEntry
                    {
                        RoleId = 4,
                        RoleName = "Admin",
                        AssignedAt = DateTime.Parse("2026-08-07T05:00:00Z").ToUniversalTime(),
                        AssignedByAdminId = 1
                    }
                ]
            },
            "roles");
    }

    [Fact]
    public void Serializer_reads_contracts_case_insensitively()
    {
        var json = _serializer.Serialize(new ProductTagsDocument
        {
            Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
        });

        var caseChanged = json
            .Replace("schemaVersion", "SchemaVersion", StringComparison.Ordinal)
            .Replace("tags", "Tags", StringComparison.Ordinal)
            .Replace("name", "Name", StringComparison.Ordinal)
            .Replace("slug", "Slug", StringComparison.Ordinal);

        var document = _serializer.Deserialize<ProductTagsDocument>(caseChanged);

        Assert.Equal("Fresh", Assert.Single(document.Tags).Name);
    }

    [Fact]
    public void Null_nested_elements_are_rejected_by_try_deserialize()
    {
        AssertNullChildRejected(
            new ProductImagesDocument
            {
                Images = [new ProductImageDocument { Url = "image", StorageKey = "image-key", IsPrimary = true, SortOrder = 1 }]
            },
            "images");
        AssertNullChildRejected(
            new ProductTagsDocument
            {
                Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
            },
            "tags");
        AssertNullChildRejected(
            new CartLinesDocument
            {
                Lines = [new CartLineDocument { ProductId = 1, Quantity = 1, Price = 2, ComboDiscount = 0 }]
            },
            "lines");
        AssertNullChildRejected(
            new OrderStatusHistoryDocument
            {
                Entries = [new OrderStatusHistoryEntry { OldStatus = OrderStatus.Pending, NewStatus = OrderStatus.Processing, AdminId = 1, CreatedAt = DateTime.UtcNow }]
            },
            "entries");
        AssertNullChildRejected(
            new OrderNotesDocument
            {
                Notes = [new OrderNoteDocument { AdminId = 1, AdminName = "Admin", Content = "Note", CreatedAt = DateTime.UtcNow }]
            },
            "notes");
        AssertNullChildRejected(
            new ComboPayload
            {
                Name = "Combo",
                Slug = "combo",
                Items = [new ComboItemPayload { ProductId = 1, Quantity = 1, SortOrder = 0 }]
            },
            "items");
        AssertNullChildRejected(CreateReturnDetailsDocument(), "items");
        AssertNullChildRejected(CreateReturnDetailsDocument(), "evidence");
        AssertNullChildRejected(CreateReturnDetailsDocument(), "events");
        AssertNullChildRejected(
            new ChatMessagesDocument
            {
                Messages = [new ChatMessageDocument { Role = "user", Content = "Hello", CreatedAt = DateTime.UtcNow }]
            },
            "messages");
        AssertNullChildRejected(
            new RolePermissionsDocument
            {
                RoleId = 1,
                Permissions = [new RolePermissionEntry { PermissionId = 1, PermissionName = "products.read", AssignedAt = DateTime.UtcNow }]
            },
            "permissions");
        AssertNullChildRejected(
            new UserRolesDocument
            {
                UserId = 1,
                Roles = [new UserRoleEntry { RoleId = 1, RoleName = "Admin", AssignedAt = DateTime.UtcNow }]
            },
            "roles");
    }

    [Fact]
    public void Nested_required_scalar_properties_are_rejected()
    {
        AssertNestedPropertiesRejected(
            new ProductImagesDocument
            {
                Images = [new ProductImageDocument { Url = "image", StorageKey = "image-key", IsPrimary = true, SortOrder = 1 }]
            },
            "images",
            "url", "storageKey", "isPrimary", "sortOrder");
        AssertNestedPropertiesRejected(
            new ProductTagsDocument
            {
                Tags = [new ProductTagDocument { Name = "Fresh", Slug = "fresh" }]
            },
            "tags",
            "name", "slug");
        AssertNestedPropertiesRejected(
            new CartLinesDocument
            {
                Lines = [new CartLineDocument { ProductId = 1, Quantity = 1, Price = 2, ComboDiscount = 0 }]
            },
            "lines",
            "productId", "quantity", "price", "comboDiscount");
        AssertNestedPropertiesRejected(
            new OrderStatusHistoryDocument
            {
                Entries = [new OrderStatusHistoryEntry { OldStatus = OrderStatus.Pending, NewStatus = OrderStatus.Processing, AdminId = 1, CreatedAt = DateTime.UtcNow }]
            },
            "entries",
            "oldStatus", "newStatus", "adminId", "createdAt");
        AssertNestedPropertiesRejected(
            new OrderNotesDocument
            {
                Notes = [new OrderNoteDocument { AdminId = 1, AdminName = "Admin", Content = "Note", CreatedAt = DateTime.UtcNow }]
            },
            "notes",
            "adminId", "adminName", "content", "createdAt");
        AssertNestedPropertiesRejected(
            new ComboPayload
            {
                Name = "Combo",
                Slug = "combo",
                Items = [new ComboItemPayload { ProductId = 1, Quantity = 1, SortOrder = 0 }]
            },
            "items",
            "productId", "quantity", "sortOrder");

        var returnDetails = CreateReturnDetailsDocument(includeRefund: true);
        AssertNestedPropertiesRejected(
            returnDetails,
            "items",
            "orderItemId", "decisionStatus", "requestedQuantity", "approvedQuantity", "reason",
            "description", "requestedAmount", "approvedAmount");
        AssertNestedPropertiesRejected(
            returnDetails,
            "evidence",
            "storageKey", "originalFileName", "contentType", "sizeBytes", "uploadedByUserId", "uploadedAtUtc");
        AssertNestedPropertiesRejected(returnDetails, "events", "eventType", "createdAtUtc");
        AssertObjectPropertiesRejected(
            returnDetails,
            "refund",
            "amount", "shippingFeeAmount", "status", "createdByUserId", "createdAtUtc");
        AssertNestedPropertiesRejected(
            new ChatMessagesDocument
            {
                Messages = [new ChatMessageDocument { Role = "user", Content = "Hello", CreatedAt = DateTime.UtcNow }]
            },
            "messages",
            "role", "content", "createdAt");
        AssertNestedPropertiesRejected(
            new RolePermissionsDocument
            {
                RoleId = 1,
                Permissions = [new RolePermissionEntry { PermissionId = 1, PermissionName = "products.read", AssignedAt = DateTime.UtcNow }]
            },
            "permissions",
            "permissionId", "permissionName", "assignedAt");
        AssertNestedPropertiesRejected(
            new UserRolesDocument
            {
                UserId = 1,
                Roles = [new UserRoleEntry { RoleId = 1, RoleName = "Admin", AssignedAt = DateTime.UtcNow }]
            },
            "roles",
            "roleId", "roleName", "assignedAt");
    }

    [Fact]
    public void Root_required_properties_are_rejected_individually()
    {
        AssertRootPropertiesRejected(
            new CouponPayload { Code = "SAVE", Type = CouponType.Percentage, Value = 10, MinOrderAmount = 20, MinQuantity = 1, UsedCount = 0, IsActive = true },
            "code", "type", "value", "minOrderAmount", "minQuantity", "usedCount", "isActive");
        AssertRootPropertiesRejected(
            new ComboPayload
            {
                Name = "Combo", Slug = "combo", IsActive = true, Status = ComboLifecycleStatus.Active,
                PricingType = ComboPricingType.SumOfItems, AllowCouponStacking = true, Revision = 1, SortOrder = 0,
                Items = []
            },
            "name", "slug", "isActive", "status", "pricingType", "allowCouponStacking", "revision", "sortOrder", "items");
        AssertRootPropertiesRejected(
            new PriceSchedulePayload
            {
                ProductId = 1, DiscountType = DiscountType.Percentage, Value = 10, StartsAt = DateTimeOffset.UtcNow,
                IsCancelled = false, Revision = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            },
            "productId", "discountType", "value", "startsAt", "isCancelled", "revision", "createdAt", "updatedAt");
        AssertRootPropertiesRejected(
            new ReviewMetadataDocument { Status = ReviewStatus.Approved, IsHidden = false, IsDeleted = false, IsVerifiedPurchase = true, HelpfulCount = 0, ReportCount = 0, CreatedAt = DateTime.UtcNow },
            "status", "isHidden", "isDeleted", "isVerifiedPurchase", "helpfulCount", "reportCount", "createdAt");
        AssertRootPropertiesRejected(
            CreateReturnDetailsDocument(),
            "status", "submittedAtUtc", "claimDeadlineAtUtc", "supplementCount", "requestedAmount", "approvedAmount",
            "approvedShippingFeeAmount", "items", "evidence", "events");
        AssertRootPropertiesRejected(
            new ContentPayload { Title = "Title", Body = "Body", Category = "general", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            "title", "body", "category", "isActive", "createdAt", "updatedAt");
        AssertRootPropertiesRejected(
            new RolePermissionsDocument { RoleId = 1, Permissions = [] },
            "roleId", "permissions");
        AssertRootPropertiesRejected(
            new UserRolesDocument { UserId = 1, Roles = [] },
            "userId", "roles");
    }

    [Fact]
    public void Undefined_enum_values_are_rejected()
    {
        AssertRootPropertyValueRejected(
            new CouponPayload { Code = "SAVE", Type = CouponType.Percentage, Value = 10, MinOrderAmount = 20, MinQuantity = 1, UsedCount = 0, IsActive = true },
            "type",
            999);
        AssertRootPropertyValueRejected(
            new ComboPayload { Name = "Combo", Slug = "combo", Status = ComboLifecycleStatus.Active, PricingType = ComboPricingType.SumOfItems, Items = [] },
            "status",
            999);
        AssertRootPropertyValueRejected(
            new ComboPayload { Name = "Combo", Slug = "combo", Status = ComboLifecycleStatus.Active, PricingType = ComboPricingType.SumOfItems, Items = [] },
            "pricingType",
            999);
        AssertRootPropertyValueRejected(
            new PriceSchedulePayload { ProductId = 1, DiscountType = DiscountType.Percentage, StartsAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            "discountType",
            999);
        AssertRootPropertyValueRejected(
            new ReviewMetadataDocument { Status = ReviewStatus.Approved, CreatedAt = DateTime.UtcNow },
            "status",
            999);
        AssertRootPropertyValueRejected(CreateReturnDetailsDocument(), "status", 999);
        AssertNestedPropertyValueRejected(
            new OrderStatusHistoryDocument
            {
                Entries = [new OrderStatusHistoryEntry { OldStatus = OrderStatus.Pending, NewStatus = OrderStatus.Processing, AdminId = 1, CreatedAt = DateTime.UtcNow }]
            },
            "entries",
            "oldStatus",
            999);
        AssertNestedPropertyValueRejected(
            CreateReturnDetailsDocument(),
            "items",
            "decisionStatus",
            999);
        AssertNestedPropertyValueRejected(
            CreateReturnDetailsDocument(),
            "items",
            "reason",
            999);
        AssertNestedPropertyValueRejected(
            CreateReturnDetailsDocument(),
            "events",
            "eventType",
            999);
    }

    [Fact]
    public void Undefined_order_status_history_new_status_is_rejected()
    {
        AssertNestedPropertyValueRejected(
            new OrderStatusHistoryDocument
            {
                Entries =
                [
                    new OrderStatusHistoryEntry
                    {
                        OldStatus = OrderStatus.Pending,
                        NewStatus = OrderStatus.Processing,
                        AdminId = 1,
                        CreatedAt = DateTime.UtcNow
                    }
                ]
            },
            "entries",
            "newStatus",
            999);
    }

    [Fact]
    public void Undefined_return_event_old_status_is_rejected()
    {
        AssertNestedPropertyValueRejected(CreateReturnDetailsDocument(), "events", "oldStatus", 999);
    }

    [Fact]
    public void Undefined_return_event_new_status_is_rejected()
    {
        AssertNestedPropertyValueRejected(CreateReturnDetailsDocument(), "events", "newStatus", 999);
    }

    [Fact]
    public void Undefined_refund_status_is_rejected()
    {
        var root = JsonNode.Parse(_serializer.Serialize(CreateReturnDetailsDocument(includeRefund: true)))!.AsObject();
        root["refund"]!["status"] = JsonValue.Create(999);

        Assert.False(
            _serializer.TryDeserialize<ReturnDetailsDocument>(root.ToJsonString(), out var invalidDocument, out var error));
        Assert.Null(invalidDocument);
        Assert.NotNull(error);
    }

    private void AssertNullChildRejected<T>(T document, string collectionProperty)
        where T : VersionedJsonDocument
    {
        var root = JsonNode.Parse(_serializer.Serialize(document))!.AsObject();
        root[collectionProperty] = JsonNode.Parse("[null]");

        Assert.False(
            _serializer.TryDeserialize<T>(root.ToJsonString(), out var invalidDocument, out var error),
            $"{typeof(T).Name}.{collectionProperty} accepted a null child.");
        Assert.Null(invalidDocument);
        Assert.NotNull(error);
    }

    private void AssertNestedPropertiesRejected<T>(T document, string collectionProperty, params string[] properties)
        where T : VersionedJsonDocument
    {
        var json = _serializer.Serialize(document);
        foreach (var property in properties)
        {
            var invalidJson = RemoveNestedProperty(json, collectionProperty, property);
            Assert.False(
                _serializer.TryDeserialize<T>(invalidJson, out var invalidDocument, out var error),
                $"{typeof(T).Name}.{collectionProperty} accepted a missing '{property}' property.");
            Assert.Null(invalidDocument);
            Assert.NotNull(error);
        }
    }

    private void AssertObjectPropertiesRejected<T>(T document, string objectProperty, params string[] properties)
        where T : VersionedJsonDocument
    {
        var json = _serializer.Serialize(document);
        foreach (var property in properties)
        {
            var invalidJson = RemoveObjectProperty(json, objectProperty, property);
            Assert.False(
                _serializer.TryDeserialize<T>(invalidJson, out var invalidDocument, out var error),
                $"{typeof(T).Name}.{objectProperty} accepted a missing '{property}' property.");
            Assert.Null(invalidDocument);
            Assert.NotNull(error);
        }
    }

    private void AssertRootPropertiesRejected<T>(T document, params string[] properties)
        where T : VersionedJsonDocument
    {
        var json = _serializer.Serialize(document);
        foreach (var property in properties)
        {
            var root = JsonNode.Parse(json)!.AsObject();
            root.Remove(property);
            Assert.False(
                _serializer.TryDeserialize<T>(root.ToJsonString(), out var invalidDocument, out var error),
                $"{typeof(T).Name} accepted a missing '{property}' property.");
            Assert.Null(invalidDocument);
            Assert.NotNull(error);
        }
    }

    private void AssertRootPropertyValueRejected<T>(T document, string property, int value)
        where T : VersionedJsonDocument
    {
        var root = JsonNode.Parse(_serializer.Serialize(document))!.AsObject();
        root[property] = JsonValue.Create(value);
        Assert.False(
            _serializer.TryDeserialize<T>(root.ToJsonString(), out var invalidDocument, out var error),
            $"{typeof(T).Name} accepted an invalid '{property}' value.");
        Assert.Null(invalidDocument);
        Assert.NotNull(error);
    }

    private void AssertNestedPropertyValueRejected<T>(T document, string collectionProperty, string property, int value)
        where T : VersionedJsonDocument
    {
        var root = JsonNode.Parse(_serializer.Serialize(document))!.AsObject();
        var child = root[collectionProperty]!.AsArray()[0]!.AsObject();
        child[property] = JsonValue.Create(value);
        Assert.False(
            _serializer.TryDeserialize<T>(root.ToJsonString(), out var invalidDocument, out var error),
            $"{typeof(T).Name}.{collectionProperty} accepted an invalid '{property}' value.");
        Assert.Null(invalidDocument);
        Assert.NotNull(error);
    }

    private static string RemoveNestedProperty(string json, string collectionProperty, string property)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root[collectionProperty]!.AsArray()[0]!.AsObject().Remove(property);
        return root.ToJsonString();
    }

    private static string RemoveObjectProperty(string json, string objectProperty, string property)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root[objectProperty]!.AsObject().Remove(property);
        return root.ToJsonString();
    }

    private static ReturnDetailsDocument CreateReturnDetailsDocument(bool includeRefund = false)
    {
        return new ReturnDetailsDocument
        {
            Status = ReturnRequestStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow,
            ClaimDeadlineAtUtc = DateTime.UtcNow.AddDays(7),
            SupplementCount = 0,
            RequestedAmount = 25,
            ApprovedAmount = 0,
            ApprovedShippingFeeAmount = 0,
            Items =
            [
                new ReturnItemDetails
                {
                    OrderItemId = 1,
                    DecisionStatus = ReturnItemDecisionStatus.Pending,
                    RequestedQuantity = 1,
                    ApprovedQuantity = 0,
                    Reason = ReturnReasonCode.Damaged,
                    Description = "Damaged",
                    RequestedAmount = 25,
                    ApprovedAmount = 0
                }
            ],
            Evidence =
            [
                new ReturnEvidenceDetails
                {
                    StorageKey = "returns/photo",
                    OriginalFileName = "photo.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = 100,
                    UploadedByUserId = 1,
                    UploadedAtUtc = DateTime.UtcNow
                }
            ],
            Events =
            [
                new ReturnEventDetails
                {
                    NewStatus = ReturnRequestStatus.Submitted,
                    EventType = ReturnEventType.Submitted,
                    CreatedAtUtc = DateTime.UtcNow
                }
            ],
            Refund = includeRefund
                ? new RefundDetails
                {
                    Amount = 25,
                    ShippingFeeAmount = 0,
                    Status = RefundStatus.Pending,
                    CreatedByUserId = 1,
                    CreatedAtUtc = DateTime.UtcNow
                }
                : null
        };
    }

    private void AssertContract<T>(T document, string requiredProperty)
        where T : VersionedJsonDocument
    {
        Assert.Equal(1, document.SchemaVersion);

        var json = _serializer.Serialize(document);
        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);

        var roundTrip = _serializer.Deserialize<T>(json);
        Assert.Equal(1, roundTrip.SchemaVersion);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(_serializer.Serialize(roundTrip))),
            $"Round-trip JSON differed for {typeof(T).Name}.");

        var requiredProperties = document.RequiredProperties
            .Append(requiredProperty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var property in requiredProperties)
        {
            var missingRequiredProperty = JsonNode.Parse(json)!.AsObject();
            missingRequiredProperty.Remove(property);
            var missingRequiredJson = missingRequiredProperty.ToJsonString();
            Assert.Throws<JsonException>(() => _serializer.Deserialize<T>(missingRequiredJson));
            Assert.False(_serializer.TryDeserialize<T>(missingRequiredJson, out var missingDocument, out var missingError));
            Assert.Null(missingDocument);
            Assert.NotNull(missingError);
        }

        Assert.Throws<JsonException>(() => _serializer.Deserialize<T>("{ malformed"));
        Assert.False(_serializer.TryDeserialize<T>("{ malformed", out var malformedDocument, out var malformedError));
        Assert.Null(malformedDocument);
        Assert.NotNull(malformedError);

        var unsupportedVersion = json.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => _serializer.Deserialize<T>(unsupportedVersion));
        Assert.False(_serializer.TryDeserialize<T>(unsupportedVersion, out var unsupportedDocument, out var unsupportedError));
        Assert.Null(unsupportedDocument);
        Assert.NotNull(unsupportedError);
    }
}
