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
        var entries = Entries ?? throw JsonDocumentValidation.Invalid("entries");
        foreach (var entry in entries)
        {
            if (entry is null)
                throw JsonDocumentValidation.Invalid("entries", "a null child");
            entry.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawEntries = JsonDocumentValidation.RequireArray(json, "entries");
        var entries = Entries ?? throw JsonDocumentValidation.Invalid("entries");
        if (entries.Count != rawEntries.GetArrayLength())
            throw JsonDocumentValidation.Invalid("entries", "an invalid child collection");

        for (var index = 0; index < rawEntries.GetArrayLength(); index++)
        {
            if (entries[index] is null)
                throw JsonDocumentValidation.Invalid("entries", "a null child");
            entries[index].Validate(rawEntries[index]);
        }
    }
}

public sealed class OrderStatusHistoryEntry
{
    private static readonly string[] RequiredPropertyNames = ["oldStatus", "newStatus", "adminId", "createdAt"];

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

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.RequireDefinedEnum(OldStatus, "oldStatus");
        JsonDocumentValidation.RequireDefinedEnum(NewStatus, "newStatus");
        Require(AdminId > 0, "adminId");
        Require(CreatedAt != default, "createdAt");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "order status history entry");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "oldStatus");
        JsonDocumentValidation.RequireNumber(json, "newStatus");
        JsonDocumentValidation.RequireNumber(json, "adminId");
        JsonDocumentValidation.RequireString(json, "createdAt");
        Validate();
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
        var notes = Notes ?? throw JsonDocumentValidation.Invalid("notes");
        foreach (var note in notes)
        {
            if (note is null)
                throw JsonDocumentValidation.Invalid("notes", "a null child");
            note.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawNotes = JsonDocumentValidation.RequireArray(json, "notes");
        var notes = Notes ?? throw JsonDocumentValidation.Invalid("notes");
        if (notes.Count != rawNotes.GetArrayLength())
            throw JsonDocumentValidation.Invalid("notes", "an invalid child collection");

        for (var index = 0; index < rawNotes.GetArrayLength(); index++)
        {
            if (notes[index] is null)
                throw JsonDocumentValidation.Invalid("notes", "a null child");
            notes[index].Validate(rawNotes[index]);
        }
    }
}

public sealed class OrderNoteDocument
{
    private static readonly string[] RequiredPropertyNames = ["adminId", "adminName", "content", "createdAt"];

    [JsonPropertyName("adminId")]
    public int AdminId { get; init; }

    [JsonPropertyName("adminName")]
    public string AdminName { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(AdminId > 0, "adminId");
        Require(!string.IsNullOrWhiteSpace(AdminName), "adminName");
        Require(!string.IsNullOrWhiteSpace(Content), "content");
        Require(CreatedAt != default, "createdAt");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "order note");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "adminId");
        JsonDocumentValidation.RequireString(json, "adminName");
        JsonDocumentValidation.RequireString(json, "content");
        JsonDocumentValidation.RequireString(json, "createdAt");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
