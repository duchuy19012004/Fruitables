using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models;

namespace Fruitables.Models.Json;

public sealed class CouponPayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames =
        ["code", "type", "value", "minOrderAmount", "minQuantity", "usedCount", "isActive"];

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public CouponType Type { get; init; }

    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("minOrderAmount")]
    public decimal MinOrderAmount { get; init; }

    [JsonPropertyName("minQuantity")]
    public decimal MinQuantity { get; init; } = 1;

    [JsonPropertyName("maxUses")]
    public int? MaxUses { get; init; }

    [JsonPropertyName("usedCount")]
    public int UsedCount { get; init; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; init; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(!string.IsNullOrWhiteSpace(Code), "code");
        Require(MinQuantity > 0, "minQuantity");
        JsonDocumentValidation.RequireDefinedEnum(Type, "type");
        Require(UsedCount >= 0, "usedCount");
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        JsonDocumentValidation.RequireString(json, "code");
        JsonDocumentValidation.RequireNumber(json, "type");
        JsonDocumentValidation.RequireNumber(json, "value");
        JsonDocumentValidation.RequireNumber(json, "minOrderAmount");
        JsonDocumentValidation.RequireNumber(json, "minQuantity");
        JsonDocumentValidation.RequireNumber(json, "usedCount");
        JsonDocumentValidation.RequireBoolean(json, "isActive");
        Validate();
    }

}

public sealed class ComboPayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames =
        ["name", "slug", "isActive", "status", "pricingType", "allowCouponStacking", "revision", "sortOrder", "items"];

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;

    [JsonPropertyName("status")]
    public ComboLifecycleStatus Status { get; init; } = ComboLifecycleStatus.Active;

    [JsonPropertyName("startsAt")]
    public DateTimeOffset? StartsAt { get; init; }

    [JsonPropertyName("endsAt")]
    public DateTimeOffset? EndsAt { get; init; }

    [JsonPropertyName("pricingType")]
    public ComboPricingType PricingType { get; init; } = ComboPricingType.SumOfItems;

    [JsonPropertyName("fixedPrice")]
    public decimal? FixedPrice { get; init; }

    [JsonPropertyName("discountValue")]
    public decimal? DiscountValue { get; init; }

    [JsonPropertyName("allowCouponStacking")]
    public bool AllowCouponStacking { get; init; } = true;

    [JsonPropertyName("revision")]
    public int Revision { get; init; } = 1;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    [JsonPropertyName("items")]
    public List<ComboItemPayload> Items { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(!string.IsNullOrWhiteSpace(Name), "name");
        Require(!string.IsNullOrWhiteSpace(Slug), "slug");
        JsonDocumentValidation.RequireDefinedEnum(Status, "status");
        JsonDocumentValidation.RequireDefinedEnum(PricingType, "pricingType");
        Require(Revision > 0, "revision");
        var items = Items ?? throw JsonDocumentValidation.Invalid("items");
        foreach (var item in items)
        {
            if (item is null)
                throw JsonDocumentValidation.Invalid("items", "a null child");
            item.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        JsonDocumentValidation.RequireString(json, "name");
        JsonDocumentValidation.RequireString(json, "slug");
        JsonDocumentValidation.RequireBoolean(json, "isActive");
        JsonDocumentValidation.RequireNumber(json, "status");
        JsonDocumentValidation.RequireNumber(json, "pricingType");
        JsonDocumentValidation.RequireBoolean(json, "allowCouponStacking");
        JsonDocumentValidation.RequireNumber(json, "revision");
        JsonDocumentValidation.RequireNumber(json, "sortOrder");

        var rawItems = JsonDocumentValidation.RequireArray(json, "items");
        var items = Items ?? throw JsonDocumentValidation.Invalid("items");
        if (items.Count != rawItems.GetArrayLength())
            throw JsonDocumentValidation.Invalid("items", "an invalid child collection");

        for (var index = 0; index < rawItems.GetArrayLength(); index++)
        {
            if (items[index] is null)
                throw JsonDocumentValidation.Invalid("items", "a null child");
            items[index].Validate(rawItems[index]);
        }
    }
}

public sealed class ComboItemPayload
{
    private static readonly string[] RequiredPropertyNames = ["productId", "quantity", "sortOrder"];

    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productVariantId")]
    public int? ProductVariantId { get; init; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; init; } = 1;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(ProductId > 0, "productId");
        Require(Quantity > 0, "quantity");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "combo item");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "productId");
        JsonDocumentValidation.RequireNumber(json, "quantity");
        JsonDocumentValidation.RequireNumber(json, "sortOrder");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class PriceSchedulePayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames =
        ["productId", "discountType", "value", "startsAt", "isCancelled", "revision", "createdAt", "updatedAt"];

    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productVariantId")]
    public int? ProductVariantId { get; init; }

    [JsonPropertyName("legacyScheduleId")]
    public int? LegacyScheduleId { get; init; }

    [JsonPropertyName("discountType")]
    public DiscountType DiscountType { get; init; }

    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("startsAt")]
    public DateTimeOffset StartsAt { get; init; }

    [JsonPropertyName("endsAt")]
    public DateTimeOffset? EndsAt { get; init; }

    [JsonPropertyName("isCancelled")]
    public bool IsCancelled { get; init; }

    [JsonPropertyName("cancelledAt")]
    public DateTimeOffset? CancelledAt { get; init; }

    [JsonPropertyName("cancelledByAdminId")]
    public int? CancelledByAdminId { get; init; }

    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; init; }

    [JsonPropertyName("revision")]
    public int Revision { get; init; } = 1;

    [JsonPropertyName("createdByAdminId")]
    public int? CreatedByAdminId { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(ProductId > 0, "productId");
        Require(StartsAt != default, "startsAt");
        Require(CreatedAt != default, "createdAt");
        Require(UpdatedAt != default, "updatedAt");
        JsonDocumentValidation.RequireDefinedEnum(DiscountType, "discountType");
        Require(Revision > 0, "revision");
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        JsonDocumentValidation.RequireNumber(json, "productId");
        JsonDocumentValidation.RequireNumber(json, "discountType");
        JsonDocumentValidation.RequireNumber(json, "value");
        JsonDocumentValidation.RequireString(json, "startsAt");
        JsonDocumentValidation.RequireBoolean(json, "isCancelled");
        JsonDocumentValidation.RequireNumber(json, "revision");
        JsonDocumentValidation.RequireString(json, "createdAt");
        JsonDocumentValidation.RequireString(json, "updatedAt");
        Validate();
    }

}
