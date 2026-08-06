using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models;

namespace Fruitables.Models.Json;

public sealed class CouponPayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["code"];

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
    }

}

public sealed class ComboPayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["name", "slug", "items"];

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
        Require(Items is not null, "items");
        foreach (var item in Items!)
            item.Validate();
    }
}

public sealed class ComboItemPayload
{
    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productVariantId")]
    public int? ProductVariantId { get; init; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; init; } = 1;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    public void Validate()
    {
        Require(ProductId > 0, "productId");
        Require(Quantity > 0, "quantity");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class PriceSchedulePayload : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["productId", "discountType"];

    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productVariantId")]
    public int? ProductVariantId { get; init; }

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
    }

}
