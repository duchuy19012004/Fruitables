using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public sealed class CartLinesDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["lines"];

    [JsonPropertyName("lines")]
    public List<CartLineDocument> Lines { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(Lines is not null, "lines");
        foreach (var line in Lines!)
            line.Validate();
    }
}

public sealed class CartLineDocument
{
    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productVariantId")]
    public int? ProductVariantId { get; init; }

    [JsonPropertyName("cartGroupId")]
    public int? CartGroupId { get; init; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("comboDiscount")]
    public decimal ComboDiscount { get; init; }

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
