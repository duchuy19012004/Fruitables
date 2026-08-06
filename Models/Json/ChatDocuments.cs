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
        Require(Messages is not null, "messages");
        foreach (var message in Messages!)
            message.Validate();
    }
}

public sealed class ChatMessageDocument
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("metadata")]
    public ChatMessageMetadata? Metadata { get; init; }

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(Role), "role");
        Require(!string.IsNullOrWhiteSpace(Content), "content");
        Require(CreatedAt != default, "createdAt");
        Metadata?.Validate();
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

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
