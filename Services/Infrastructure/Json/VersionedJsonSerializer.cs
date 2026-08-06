using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models.Json;

namespace Fruitables.Services.Infrastructure.Json;

public sealed class VersionedJsonSerializer : IJsonDocumentSerializer
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string Serialize<T>(T document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        if (document is not VersionedJsonDocument versionedDocument)
            throw new JsonException($"Type {typeof(T).Name} is not a versioned JSON document.");

        ValidateVersion(versionedDocument.SchemaVersion);
        versionedDocument.Validate();

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new JsonException("JSON document is empty.");

        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("JSON document must be an object.");

            var schemaVersion = ReadSchemaVersion(parsed.RootElement);
            ValidateVersion(schemaVersion);

            var document = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (document is null)
                throw new JsonException("JSON document deserialized to null.");

            if (document is not VersionedJsonDocument versionedDocument)
                throw new JsonException($"Type {typeof(T).Name} is not a versioned JSON document.");

            foreach (var requiredProperty in versionedDocument.RequiredProperties)
            {
                if (!HasProperty(parsed.RootElement, requiredProperty))
                    throw new JsonException($"Required JSON property '{requiredProperty}' is missing.");
            }

            ValidateVersion(versionedDocument.SchemaVersion);
            versionedDocument.Validate();
            return document;
        }
        catch (JsonException ex) when (ex.GetType() != typeof(JsonException))
        {
            throw new JsonException("JSON document is malformed or invalid.", ex);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (NotSupportedException ex)
        {
            throw new JsonException("JSON document could not be deserialized.", ex);
        }
        catch (FormatException ex)
        {
            throw new JsonException("JSON document has an invalid format.", ex);
        }
    }

    public bool TryDeserialize<T>(string json, out T? document, out string? error)
    {
        try
        {
            document = Deserialize<T>(json);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
        {
            document = default;
            error = ex.Message;
            return false;
        }
    }

    private static int ReadSchemaVersion(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "schemaVersion", StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var version))
                throw new JsonException("schemaVersion must be an integer.");

            return version;
        }

        throw new JsonException("Required JSON property 'schemaVersion' is missing.");
    }

    private static bool HasProperty(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ValidateVersion(int schemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new JsonException($"Unsupported schema version: {schemaVersion}.");
    }
}
