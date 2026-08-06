using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models;

namespace Fruitables.Models.Json;

public sealed class ReviewMetadataDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["status"];

    [JsonPropertyName("status")]
    public ReviewStatus Status { get; init; }

    [JsonPropertyName("isHidden")]
    public bool IsHidden { get; init; }

    [JsonPropertyName("hiddenReason")]
    public string? HiddenReason { get; init; }

    [JsonPropertyName("hiddenByAdminId")]
    public int? HiddenByAdminId { get; init; }

    [JsonPropertyName("hiddenAt")]
    public DateTime? HiddenAt { get; init; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; init; }

    [JsonPropertyName("deletedByAdminId")]
    public int? DeletedByAdminId { get; init; }

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; init; }

    [JsonPropertyName("isVerifiedPurchase")]
    public bool IsVerifiedPurchase { get; init; }

    [JsonPropertyName("helpfulCount")]
    public int HelpfulCount { get; init; }

    [JsonPropertyName("reportCount")]
    public int ReportCount { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; init; }

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(CreatedAt != default, "createdAt");
        Require(HelpfulCount >= 0, "helpfulCount");
        Require(ReportCount >= 0, "reportCount");
    }

}
