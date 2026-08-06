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

        var missingRequiredProperty = JsonNode.Parse(json)!.AsObject();
        missingRequiredProperty.Remove(requiredProperty);
        var missingRequiredJson = missingRequiredProperty.ToJsonString();
        Assert.Throws<JsonException>(() => _serializer.Deserialize<T>(missingRequiredJson));
        Assert.False(_serializer.TryDeserialize<T>(missingRequiredJson, out var missingDocument, out var missingError));
        Assert.Null(missingDocument);
        Assert.NotNull(missingError);

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
