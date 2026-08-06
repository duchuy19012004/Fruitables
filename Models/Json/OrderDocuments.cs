using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models;

namespace Fruitables.Models.Json;

public sealed class OrderStatusHistoryDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["entries"];

    [JsonPropertyName("entries")]
    public List<OrderStatusHistoryEntry> Entries { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(Entries is not null, "entries");
        foreach (var entry in Entries!)
            entry.Validate();
    }
}

public sealed class OrderStatusHistoryEntry
{
    [JsonPropertyName("oldStatus")]
    public OrderStatus OldStatus { get; init; }

    [JsonPropertyName("newStatus")]
    public OrderStatus NewStatus { get; init; }

    [JsonPropertyName("adminId")]
    public int AdminId { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    public void Validate()
    {
        Require(AdminId > 0, "adminId");
        Require(CreatedAt != default, "createdAt");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class OrderNotesDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["notes"];

    [JsonPropertyName("notes")]
    public List<OrderNoteDocument> Notes { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(Notes is not null, "notes");
        foreach (var note in Notes!)
            note.Validate();
    }
}

public sealed class OrderNoteDocument
{
    [JsonPropertyName("adminId")]
    public int AdminId { get; init; }

    [JsonPropertyName("adminName")]
    public string AdminName { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    public void Validate()
    {
        Require(AdminId > 0, "adminId");
        Require(!string.IsNullOrWhiteSpace(AdminName), "adminName");
        Require(!string.IsNullOrWhiteSpace(Content), "content");
        Require(CreatedAt != default, "createdAt");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
