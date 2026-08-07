using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models.Returns;

namespace Fruitables.Models.Json;

public sealed class ReturnDetailsDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames =
        ["status", "submittedAtUtc", "claimDeadlineAtUtc", "supplementCount", "requestedAmount", "approvedAmount", "approvedShippingFeeAmount", "items", "evidence", "events"];

    [JsonPropertyName("status")]
    public ReturnRequestStatus Status { get; init; }

    [JsonPropertyName("submittedAtUtc")]
    public DateTime SubmittedAtUtc { get; init; }

    [JsonPropertyName("claimDeadlineAtUtc")]
    public DateTime ClaimDeadlineAtUtc { get; init; }

    [JsonPropertyName("supplementDeadlineAtUtc")]
    public DateTime? SupplementDeadlineAtUtc { get; init; }

    [JsonPropertyName("supplementCount")]
    public int SupplementCount { get; init; }

    [JsonPropertyName("requestedAmount")]
    public decimal RequestedAmount { get; init; }

    [JsonPropertyName("approvedAmount")]
    public decimal ApprovedAmount { get; init; }

    [JsonPropertyName("approvedShippingFeeAmount")]
    public decimal ApprovedShippingFeeAmount { get; init; }

    [JsonPropertyName("customerNote")]
    public string? CustomerNote { get; init; }

    [JsonPropertyName("adminNote")]
    public string? AdminNote { get; init; }

    [JsonPropertyName("items")]
    public List<ReturnItemDetails> Items { get; init; } = [];

    [JsonPropertyName("evidence")]
    public List<ReturnEvidenceDetails> Evidence { get; init; } = [];

    [JsonPropertyName("events")]
    public List<ReturnEventDetails> Events { get; init; } = [];

    [JsonPropertyName("refund")]
    public RefundDetails? Refund { get; init; }

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        JsonDocumentValidation.RequireDefinedEnum(Status, "status");
        Require(SubmittedAtUtc != default, "submittedAtUtc");
        Require(ClaimDeadlineAtUtc != default, "claimDeadlineAtUtc");
        var items = Items ?? throw JsonDocumentValidation.Invalid("items");
        foreach (var item in items)
        {
            if (item is null)
                throw JsonDocumentValidation.Invalid("items", "a null child");
            item.Validate();
        }
        var evidence = Evidence ?? throw JsonDocumentValidation.Invalid("evidence");
        foreach (var entry in evidence)
        {
            if (entry is null)
                throw JsonDocumentValidation.Invalid("evidence", "a null child");
            entry.Validate();
        }
        var events = Events ?? throw JsonDocumentValidation.Invalid("events");
        foreach (var entry in events)
        {
            if (entry is null)
                throw JsonDocumentValidation.Invalid("events", "a null child");
            entry.Validate();
        }
        Refund?.Validate();
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        JsonDocumentValidation.RequireNumber(json, "status");
        JsonDocumentValidation.RequireString(json, "submittedAtUtc");
        JsonDocumentValidation.RequireString(json, "claimDeadlineAtUtc");
        JsonDocumentValidation.RequireNumber(json, "supplementCount");
        JsonDocumentValidation.RequireNumber(json, "requestedAmount");
        JsonDocumentValidation.RequireNumber(json, "approvedAmount");
        JsonDocumentValidation.RequireNumber(json, "approvedShippingFeeAmount");

        var rawItems = JsonDocumentValidation.RequireArray(json, "items");
        var items = Items ?? throw JsonDocumentValidation.Invalid("items");
        if (items.Count != rawItems.GetArrayLength())
            throw JsonDocumentValidation.Invalid("items", "an invalid child collection");
        for (var index = 0; index < rawItems.GetArrayLength(); index++)
        {
            if (items[index] is null)
                throw JsonDocumentValidation.Invalid("items", "a null child");
            items[index].Validate(rawItems[index]);
        }

        var rawEvidence = JsonDocumentValidation.RequireArray(json, "evidence");
        var evidence = Evidence ?? throw JsonDocumentValidation.Invalid("evidence");
        if (evidence.Count != rawEvidence.GetArrayLength())
            throw JsonDocumentValidation.Invalid("evidence", "an invalid child collection");
        for (var index = 0; index < rawEvidence.GetArrayLength(); index++)
        {
            if (evidence[index] is null)
                throw JsonDocumentValidation.Invalid("evidence", "a null child");
            evidence[index].Validate(rawEvidence[index]);
        }

        var rawEvents = JsonDocumentValidation.RequireArray(json, "events");
        var events = Events ?? throw JsonDocumentValidation.Invalid("events");
        if (events.Count != rawEvents.GetArrayLength())
            throw JsonDocumentValidation.Invalid("events", "an invalid child collection");
        for (var index = 0; index < rawEvents.GetArrayLength(); index++)
        {
            if (events[index] is null)
                throw JsonDocumentValidation.Invalid("events", "a null child");
            events[index].Validate(rawEvents[index]);
        }

        if (JsonDocumentValidation.TryGetProperty(json, "refund", out var rawRefund)
            && rawRefund.ValueKind != JsonValueKind.Null)
        {
            if (Refund is null)
                throw JsonDocumentValidation.Invalid("refund", "a null child");
            Refund.Validate(rawRefund);
        }
    }

}

public sealed class ReturnItemDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    private static readonly string[] RequiredPropertyNames =
        ["orderItemId", "decisionStatus", "requestedQuantity", "approvedQuantity", "reason", "description", "requestedAmount", "approvedAmount"];

    [JsonPropertyName("orderItemId")]
    public int OrderItemId { get; init; }

    [JsonPropertyName("decisionStatus")]
    public ReturnItemDecisionStatus DecisionStatus { get; init; }

    [JsonPropertyName("requestedQuantity")]
    public decimal RequestedQuantity { get; init; }

    [JsonPropertyName("approvedQuantity")]
    public decimal ApprovedQuantity { get; init; }

    [JsonPropertyName("reason")]
    public ReturnReasonCode Reason { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("decisionReason")]
    public string? DecisionReason { get; init; }

    [JsonPropertyName("requestedAmount")]
    public decimal RequestedAmount { get; init; }

    [JsonPropertyName("approvedAmount")]
    public decimal ApprovedAmount { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.RequireDefinedEnum(DecisionStatus, "decisionStatus");
        JsonDocumentValidation.RequireDefinedEnum(Reason, "reason");
        Require(OrderItemId > 0, "orderItemId");
        Require(RequestedQuantity > 0, "requestedQuantity");
        Require(!string.IsNullOrWhiteSpace(Description), "description");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "return item");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "orderItemId");
        JsonDocumentValidation.RequireNumber(json, "decisionStatus");
        JsonDocumentValidation.RequireNumber(json, "requestedQuantity");
        JsonDocumentValidation.RequireNumber(json, "approvedQuantity");
        JsonDocumentValidation.RequireNumber(json, "reason");
        JsonDocumentValidation.RequireString(json, "description");
        JsonDocumentValidation.RequireNumber(json, "requestedAmount");
        JsonDocumentValidation.RequireNumber(json, "approvedAmount");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ReturnEvidenceDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    private static readonly string[] RequiredPropertyNames =
        ["storageKey", "originalFileName", "contentType", "sizeBytes", "uploadedByUserId", "uploadedAtUtc"];

    [JsonPropertyName("storageKey")]
    public string StorageKey { get; init; } = string.Empty;

    [JsonPropertyName("returnRequestItemId")]
    public int? ReturnRequestItemId { get; init; }

    [JsonPropertyName("originalFileName")]
    public string OriginalFileName { get; init; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("uploadedByUserId")]
    public int UploadedByUserId { get; init; }

    [JsonPropertyName("uploadedAtUtc")]
    public DateTime UploadedAtUtc { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(StorageKey), "storageKey");
        Require(!string.IsNullOrWhiteSpace(OriginalFileName), "originalFileName");
        Require(!string.IsNullOrWhiteSpace(ContentType), "contentType");
        Require(SizeBytes >= 0, "sizeBytes");
        Require(UploadedByUserId > 0, "uploadedByUserId");
        Require(UploadedAtUtc != default, "uploadedAtUtc");
        if (ReturnRequestItemId.HasValue)
            Require(ReturnRequestItemId.Value > 0, "returnRequestItemId");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "return evidence");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireString(json, "storageKey");
        if (JsonDocumentValidation.TryGetProperty(json, "returnRequestItemId", out var rawItemId)
            && rawItemId.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
            throw JsonDocumentValidation.Invalid("returnRequestItemId", "not a number");
        JsonDocumentValidation.RequireString(json, "originalFileName");
        JsonDocumentValidation.RequireString(json, "contentType");
        JsonDocumentValidation.RequireNumber(json, "sizeBytes");
        JsonDocumentValidation.RequireNumber(json, "uploadedByUserId");
        JsonDocumentValidation.RequireString(json, "uploadedAtUtc");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ReturnEventDetails
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    private static readonly string[] RequiredPropertyNames = ["eventType", "createdAtUtc"];

    [JsonPropertyName("oldStatus")]
    public ReturnRequestStatus? OldStatus { get; init; }

    [JsonPropertyName("newStatus")]
    public ReturnRequestStatus? NewStatus { get; init; }

    [JsonPropertyName("eventType")]
    public ReturnEventType EventType { get; init; }

    [JsonPropertyName("actorUserId")]
    public int? ActorUserId { get; init; }

    [JsonPropertyName("returnRequestItemId")]
    public int? ReturnRequestItemId { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.RequireDefinedEnum(EventType, "eventType");
        if (OldStatus.HasValue)
            JsonDocumentValidation.RequireDefinedEnum(OldStatus.Value, "oldStatus");
        if (NewStatus.HasValue)
            JsonDocumentValidation.RequireDefinedEnum(NewStatus.Value, "newStatus");
        Require(CreatedAtUtc != default, "createdAtUtc");
        if (ReturnRequestItemId.HasValue)
            Require(ReturnRequestItemId.Value > 0, "returnRequestItemId");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "return event");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "eventType");
        JsonDocumentValidation.RequireString(json, "createdAtUtc");
        if (JsonDocumentValidation.TryGetProperty(json, "returnRequestItemId", out var rawItemId)
            && rawItemId.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
            throw JsonDocumentValidation.Invalid("returnRequestItemId", "not a number");
        JsonDocumentValidation.RequireOptionalEnum(json, "oldStatus", OldStatus);
        JsonDocumentValidation.RequireOptionalEnum(json, "newStatus", NewStatus);
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class RefundDetails
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    private static readonly string[] RequiredPropertyNames = ["amount", "shippingFeeAmount", "status", "createdByUserId", "createdAtUtc"];

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("shippingFeeAmount")]
    public decimal ShippingFeeAmount { get; init; }

    [JsonPropertyName("status")]
    public RefundStatus Status { get; init; }

    [JsonPropertyName("transactionReference")]
    public string? TransactionReference { get; init; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; init; }

    [JsonPropertyName("createdByUserId")]
    public int CreatedByUserId { get; init; }

    [JsonPropertyName("processedByUserId")]
    public int? ProcessedByUserId { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("processedAtUtc")]
    public DateTime? ProcessedAtUtc { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        JsonDocumentValidation.RequireDefinedEnum(Status, "status");
        Require(CreatedByUserId > 0, "createdByUserId");
        Require(CreatedAtUtc != default, "createdAtUtc");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "refund");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "amount");
        JsonDocumentValidation.RequireNumber(json, "shippingFeeAmount");
        JsonDocumentValidation.RequireNumber(json, "status");
        JsonDocumentValidation.RequireNumber(json, "createdByUserId");
        JsonDocumentValidation.RequireString(json, "createdAtUtc");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
