using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public abstract class VersionedJsonDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonIgnore]
    public abstract IReadOnlyCollection<string> RequiredProperties { get; }

    public virtual void Validate()
    {
        if (SchemaVersion != 1)
            throw new JsonException($"Unsupported schema version: {SchemaVersion}.");
    }

    protected static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ProductImagesDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["images"];

    [JsonPropertyName("images")]
    public List<ProductImageDocument> Images { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(Images is not null, "images");
        foreach (var image in Images!)
            image.Validate();
    }
}

public sealed class ProductImageDocument
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("storageKey")]
    public string StorageKey { get; init; } = string.Empty;

    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; init; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Url), "url");
        Require(!string.IsNullOrWhiteSpace(StorageKey), "storageKey");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ProductTagsDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["tags"];

    [JsonPropertyName("tags")]
    public List<ProductTagDocument> Tags { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(Tags is not null, "tags");
        foreach (var tag in Tags!)
            tag.Validate();
    }
}

public sealed class ProductTagDocument
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Name), "name");
        Require(!string.IsNullOrWhiteSpace(Slug), "slug");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
