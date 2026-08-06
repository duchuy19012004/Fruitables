using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public sealed class ChatMessagesDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["messages"];

    [JsonPropertyName("messages")]
    public List<ChatMessageDocument> Messages { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        var messages = Messages ?? throw JsonDocumentValidation.Invalid("messages");
        foreach (var message in messages)
        {
            if (message is null)
                throw JsonDocumentValidation.Invalid("messages", "a null child");
            message.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawMessages = JsonDocumentValidation.RequireArray(json, "messages");
        var messages = Messages ?? throw JsonDocumentValidation.Invalid("messages");
        if (messages.Count != rawMessages.GetArrayLength())
            throw JsonDocumentValidation.Invalid("messages", "an invalid child collection");

        for (var index = 0; index < rawMessages.GetArrayLength(); index++)
        {
            if (messages[index] is null)
                throw JsonDocumentValidation.Invalid("messages", "a null child");
            messages[index].Validate(rawMessages[index]);
        }
    }
}

public sealed class ChatMessageDocument
{
    private static readonly string[] RequiredPropertyNames = ["role", "content", "createdAt"];

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("metadata")]
    public ChatMessageMetadata? Metadata { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Role), "role");
        Require(!string.IsNullOrWhiteSpace(Content), "content");
        Require(CreatedAt != default, "createdAt");
        Metadata?.Validate();
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "chat message");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireString(json, "role");
        JsonDocumentValidation.RequireString(json, "content");
        JsonDocumentValidation.RequireString(json, "createdAt");
        if (JsonDocumentValidation.TryGetProperty(json, "metadata", out var rawMetadata)
            && rawMetadata.ValueKind != JsonValueKind.Null)
        {
            if (Metadata is null)
                throw JsonDocumentValidation.Invalid("metadata", "a null child");
            Metadata.Validate(rawMetadata);
        }
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ChatMessageMetadata
{
    [JsonPropertyName("refused")]
    public bool? Refused { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    public void Validate()
    {
        if (Action is not null)
            Require(!string.IsNullOrWhiteSpace(Action), "action");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "chat metadata");
        if (JsonDocumentValidation.TryGetProperty(json, "refused", out var refused)
            && refused.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
            throw JsonDocumentValidation.Invalid("refused", "not a boolean");
        if (JsonDocumentValidation.TryGetProperty(json, "action", out var action)
            && action.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw JsonDocumentValidation.Invalid("action", "not a string");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
