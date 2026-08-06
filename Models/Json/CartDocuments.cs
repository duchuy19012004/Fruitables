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
        var lines = Lines ?? throw JsonDocumentValidation.Invalid("lines");
        foreach (var line in lines)
        {
            if (line is null)
                throw JsonDocumentValidation.Invalid("lines", "a null child");
            line.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawLines = JsonDocumentValidation.RequireArray(json, "lines");
        var lines = Lines ?? throw JsonDocumentValidation.Invalid("lines");
        if (lines.Count != rawLines.GetArrayLength())
            throw JsonDocumentValidation.Invalid("lines", "an invalid child collection");

        for (var index = 0; index < rawLines.GetArrayLength(); index++)
        {
            if (lines[index] is null)
                throw JsonDocumentValidation.Invalid("lines", "a null child");
            lines[index].Validate(rawLines[index]);
        }
    }
}

public sealed class CartLineDocument
{
    private static readonly string[] RequiredPropertyNames = ["productId", "quantity", "price", "comboDiscount"];

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

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(ProductId > 0, "productId");
        Require(Quantity > 0, "quantity");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "cart line");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "productId");
        JsonDocumentValidation.RequireNumber(json, "quantity");
        JsonDocumentValidation.RequireNumber(json, "price");
        JsonDocumentValidation.RequireNumber(json, "comboDiscount");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
