using System.Text.Json;
using System.Text.Json.Serialization;
using Fruitables.Models;

namespace Fruitables.Models.Json;

public sealed class ReviewMetadataDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames =
        ["status", "isHidden", "isDeleted", "isVerifiedPurchase", "helpfulCount", "reportCount", "createdAt"];

    public ReviewMetadataDocument With(
        ReviewStatus? status = null,
        bool? isHidden = null,
        string? hiddenReason = null,
        bool hiddenReasonSet = false,
        int? hiddenByAdminId = null,
        bool hiddenByAdminIdSet = false,
        DateTime? hiddenAt = null,
        bool hiddenAtSet = false,
        bool? isDeleted = null,
        int? deletedByAdminId = null,
        bool deletedByAdminIdSet = false,
        DateTime? deletedAt = null,
        bool deletedAtSet = false,
        bool? isVerifiedPurchase = null,
        int? helpfulCount = null,
        int? reportCount = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null,
        bool updatedAtSet = false,
        List<int>? helpfulUserIds = null,
        List<ReviewReportEntry>? reports = null,
        ReviewSentimentPayload? sentiment = null,
        bool sentimentSet = false) =>
        new()
        {
            SchemaVersion = SchemaVersion,
            Status = status ?? Status,
            IsHidden = isHidden ?? IsHidden,
            HiddenReason = hiddenReasonSet ? hiddenReason : HiddenReason,
            HiddenByAdminId = hiddenByAdminIdSet ? hiddenByAdminId : HiddenByAdminId,
            HiddenAt = hiddenAtSet ? hiddenAt : HiddenAt,
            IsDeleted = isDeleted ?? IsDeleted,
            DeletedByAdminId = deletedByAdminIdSet ? deletedByAdminId : DeletedByAdminId,
            DeletedAt = deletedAtSet ? deletedAt : DeletedAt,
            IsVerifiedPurchase = isVerifiedPurchase ?? IsVerifiedPurchase,
            HelpfulCount = helpfulCount ?? HelpfulCount,
            ReportCount = reportCount ?? ReportCount,
            CreatedAt = createdAt ?? CreatedAt,
            UpdatedAt = updatedAtSet ? updatedAt : UpdatedAt,
            HelpfulUserIds = helpfulUserIds ?? HelpfulUserIds,
            Reports = reports ?? Reports,
            Sentiment = sentimentSet ? sentiment : Sentiment
        };

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

    [JsonPropertyName("helpfulUserIds")]
    public List<int> HelpfulUserIds { get; init; } = [];

    [JsonPropertyName("reports")]
    public List<ReviewReportEntry> Reports { get; init; } = [];

    [JsonPropertyName("sentiment")]
    public ReviewSentimentPayload? Sentiment { get; init; }

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        JsonDocumentValidation.RequireDefinedEnum(Status, "status");
        Require(CreatedAt != default, "createdAt");
        Require(HelpfulCount >= 0, "helpfulCount");
        Require(ReportCount >= 0, "reportCount");
        foreach (var report in Reports ?? [])
            report.Validate();
        Sentiment?.Validate();
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        JsonDocumentValidation.RequireNumber(json, "status");
        JsonDocumentValidation.RequireBoolean(json, "isHidden");
        JsonDocumentValidation.RequireBoolean(json, "isDeleted");
        JsonDocumentValidation.RequireBoolean(json, "isVerifiedPurchase");
        JsonDocumentValidation.RequireNumber(json, "helpfulCount");
        JsonDocumentValidation.RequireNumber(json, "reportCount");
        JsonDocumentValidation.RequireString(json, "createdAt");
        Validate();
    }
}

public sealed class ReviewReportEntry
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("reportedByUserId")]
    public int ReportedByUserId { get; init; }

    [JsonPropertyName("reason")]
    public ReportReason Reason { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public ReportStatus Status { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    public void Validate()
    {
        JsonDocumentValidation.Require(ReportedByUserId > 0, "reportedByUserId");
        JsonDocumentValidation.RequireDefinedEnum(Reason, "reason");
        JsonDocumentValidation.RequireDefinedEnum(Status, "status");
        JsonDocumentValidation.Require(CreatedAt != default, "createdAt");
    }
}

public sealed class ReviewSentimentPayload
{
    [JsonPropertyName("sentiment")]
    public SentimentLabel Sentiment { get; init; }

    [JsonPropertyName("ratingSentiment")]
    public SentimentLabel RatingSentiment { get; init; }

    [JsonPropertyName("commentSentiment")]
    public SentimentLabel CommentSentiment { get; init; }

    [JsonPropertyName("hasRatingCommentConflict")]
    public bool HasRatingCommentConflict { get; init; }

    [JsonPropertyName("needsManualReview")]
    public bool NeedsManualReview { get; init; }

    [JsonPropertyName("aspects")]
    public List<ReviewSentimentAspectPayload> Aspects { get; init; } = [];

    public void Validate()
    {
        JsonDocumentValidation.RequireDefinedEnum(Sentiment, "sentiment");
        JsonDocumentValidation.RequireDefinedEnum(RatingSentiment, "ratingSentiment");
        JsonDocumentValidation.RequireDefinedEnum(CommentSentiment, "commentSentiment");
        foreach (var aspect in Aspects ?? [])
            aspect.Validate();
    }
}

public sealed class ReviewSentimentAspectPayload
{
    [JsonPropertyName("aspect")]
    public string Aspect { get; init; } = string.Empty;

    [JsonPropertyName("sentiment")]
    public SentimentLabel Sentiment { get; init; }

    public void Validate()
    {
        JsonDocumentValidation.Require(!string.IsNullOrWhiteSpace(Aspect), "aspect");
        JsonDocumentValidation.RequireDefinedEnum(Sentiment, "sentiment");
    }
}
