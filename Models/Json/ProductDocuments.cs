using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public abstract class VersionedJsonDocument
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonIgnore]
    public abstract IReadOnlyCollection<string> RequiredProperties { get; }

    public virtual void Validate()
    {
        RequireSupportedSchemaVersion(SchemaVersion);
    }

    internal static void RequireSupportedSchemaVersion(int schemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new JsonException($"Unsupported schema version: {schemaVersion}.");
    }

    internal virtual void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "document");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        Validate();
    }

    protected static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

internal static class JsonDocumentValidation
{
    internal static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw Invalid(propertyName);
    }

    internal static JsonException Invalid(string propertyName, string detail = "missing or invalid") =>
        new($"Required JSON property '{propertyName}' is {detail}.");

    internal static void RequireObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid(propertyName, "not an object");
    }

    internal static void RequireProperties(JsonElement element, IEnumerable<string> propertyNames)
    {
        RequireObject(element, "document");
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out _))
                throw Invalid(propertyName);
        }
    }

    internal static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    internal static JsonElement RequireProperty(JsonElement element, string propertyName, JsonValueKind expectedKind)
    {
        if (!TryGetProperty(element, propertyName, out var value))
            throw Invalid(propertyName);
        if (value.ValueKind != expectedKind)
            throw Invalid(propertyName, $"not {expectedKind}");
        return value;
    }

    internal static JsonElement RequireArray(JsonElement element, string propertyName) =>
        RequireProperty(element, propertyName, JsonValueKind.Array);

    internal static JsonElement RequireNumber(JsonElement element, string propertyName) =>
        RequireProperty(element, propertyName, JsonValueKind.Number);

    internal static JsonElement RequireString(JsonElement element, string propertyName) =>
        RequireProperty(element, propertyName, JsonValueKind.String);

    internal static JsonElement RequireBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw Invalid(propertyName, "not a boolean");
        return value;
    }

    internal static void RequireDefinedEnum<TEnum>(TEnum value, string propertyName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw Invalid(propertyName, "an undefined enum value");
    }

    internal static void RequireOptionalEnum<TEnum>(JsonElement element, string propertyName, TEnum? value)
        where TEnum : struct, Enum
    {
        if (!TryGetProperty(element, propertyName, out var rawValue) || rawValue.ValueKind == JsonValueKind.Null)
            return;
        if (rawValue.ValueKind != JsonValueKind.Number)
            throw Invalid(propertyName, "not an enum number");
        if (!value.HasValue || !Enum.IsDefined(value.Value))
            throw Invalid(propertyName, "an undefined enum value");
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
        var images = Images ?? throw JsonDocumentValidation.Invalid("images");
        foreach (var image in images)
        {
            if (image is null)
                throw JsonDocumentValidation.Invalid("images", "a null child");
            image.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawImages = JsonDocumentValidation.RequireArray(json, "images");
        var images = Images ?? throw JsonDocumentValidation.Invalid("images");
        if (images.Count != rawImages.GetArrayLength())
            throw JsonDocumentValidation.Invalid("images", "an invalid child collection");

        for (var index = 0; index < rawImages.GetArrayLength(); index++)
        {
            if (images[index] is null)
                throw JsonDocumentValidation.Invalid("images", "a null child");
            images[index].Validate(rawImages[index]);
        }
    }
}

public sealed class ProductImageDocument
{
    private static readonly string[] RequiredPropertyNames = ["url", "storageKey", "isPrimary", "sortOrder"];

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("storageKey")]
    public string StorageKey { get; init; } = string.Empty;

    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; init; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.Require(Id >= 0, "id");
        JsonDocumentValidation.Require(!string.IsNullOrWhiteSpace(Url), "url");
        JsonDocumentValidation.Require(!string.IsNullOrWhiteSpace(StorageKey), "storageKey");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "image");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireString(json, "url");
        JsonDocumentValidation.RequireString(json, "storageKey");
        JsonDocumentValidation.RequireBoolean(json, "isPrimary");
        JsonDocumentValidation.RequireNumber(json, "sortOrder");
        JsonDocumentValidation.Require(Id >= 0, "id");
        Validate();
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
        var tags = Tags ?? throw JsonDocumentValidation.Invalid("tags");
        foreach (var tag in tags)
        {
            if (tag is null)
                throw JsonDocumentValidation.Invalid("tags", "a null child");
            tag.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawTags = JsonDocumentValidation.RequireArray(json, "tags");
        var tags = Tags ?? throw JsonDocumentValidation.Invalid("tags");
        if (tags.Count != rawTags.GetArrayLength())
            throw JsonDocumentValidation.Invalid("tags", "an invalid child collection");

        for (var index = 0; index < rawTags.GetArrayLength(); index++)
        {
            if (tags[index] is null)
                throw JsonDocumentValidation.Invalid("tags", "a null child");
            tags[index].Validate(rawTags[index]);
        }
    }
}

public sealed class ProductTagDocument
{
    private static readonly string[] RequiredPropertyNames = ["name", "slug"];

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.Require(!string.IsNullOrWhiteSpace(Name), "name");
        JsonDocumentValidation.Require(!string.IsNullOrWhiteSpace(Slug), "slug");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "tag");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireString(json, "name");
        JsonDocumentValidation.RequireString(json, "slug");
        Validate();
    }
}
