using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models.Returns;

namespace Fruitables.Models.Json;

public sealed class ReturnDetailsDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["status", "items", "evidence", "events"];

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
        Require(SubmittedAtUtc != default, "submittedAtUtc");
        Require(ClaimDeadlineAtUtc != default, "claimDeadlineAtUtc");
        Require(Items is not null, "items");
        Require(Evidence is not null, "evidence");
        Require(Events is not null, "events");
        foreach (var item in Items!)
            item.Validate();
        foreach (var evidence in Evidence!)
            evidence.Validate();
        foreach (var entry in Events!)
            entry.Validate();
        Refund?.Validate();
    }

}

public sealed class ReturnItemDetails
{
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

    public void Validate()
    {
        Require(OrderItemId > 0, "orderItemId");
        Require(RequestedQuantity > 0, "requestedQuantity");
        Require(!string.IsNullOrWhiteSpace(Description), "description");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ReturnEvidenceDetails
{
    [JsonPropertyName("storageKey")]
    public string StorageKey { get; init; } = string.Empty;

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

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(StorageKey), "storageKey");
        Require(!string.IsNullOrWhiteSpace(OriginalFileName), "originalFileName");
        Require(!string.IsNullOrWhiteSpace(ContentType), "contentType");
        Require(SizeBytes >= 0, "sizeBytes");
        Require(UploadedByUserId > 0, "uploadedByUserId");
        Require(UploadedAtUtc != default, "uploadedAtUtc");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class ReturnEventDetails
{
    [JsonPropertyName("oldStatus")]
    public ReturnRequestStatus? OldStatus { get; init; }

    [JsonPropertyName("newStatus")]
    public ReturnRequestStatus? NewStatus { get; init; }

    [JsonPropertyName("eventType")]
    public ReturnEventType EventType { get; init; }

    [JsonPropertyName("actorUserId")]
    public int? ActorUserId { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    public void Validate()
    {
        Require(CreatedAtUtc != default, "createdAtUtc");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class RefundDetails
{
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

    public void Validate()
    {
        Require(CreatedByUserId > 0, "createdByUserId");
        Require(CreatedAtUtc != default, "createdAtUtc");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
