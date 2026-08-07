using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;

namespace Fruitables.Services.Reviews;

internal static class ReviewAggregateJson
{
    public static ReviewMetadataDocument Read(Review review, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(review.MetadataJson) ||
            review.MetadataJson.Trim() is "[]" or "{}" or """{ "schemaVersion": 1 }""")
        {
            return FromRelational(review);
        }

        try
        {
            return serializer.Deserialize<ReviewMetadataDocument>(review.MetadataJson);
        }
        catch
        {
            return FromRelational(review);
        }
    }

    public static void Write(Review review, ReviewMetadataDocument document, IJsonDocumentSerializer serializer)
    {
        review.MetadataJson = serializer.Serialize(document);
        review.Status = document.Status;
        review.IsHidden = document.IsHidden;
        review.HiddenReason = document.HiddenReason;
        review.HiddenByAdminId = document.HiddenByAdminId;
        review.HiddenAt = document.HiddenAt;
        review.IsDeleted = document.IsDeleted;
        review.DeletedByAdminId = document.DeletedByAdminId;
        review.DeletedAt = document.DeletedAt;
        review.IsVerifiedPurchase = document.IsVerifiedPurchase;
        review.HelpfulCount = document.HelpfulCount;
        review.ReportCount = document.ReportCount;
        review.UpdatedAt = document.UpdatedAt ?? DateTime.UtcNow;
        review.RowVersion = Guid.NewGuid().ToByteArray();
    }

    public static ReviewMetadataDocument FromRelational(Review review) =>
        new()
        {
            Status = review.Status,
            IsHidden = review.IsHidden,
            HiddenReason = review.HiddenReason,
            HiddenByAdminId = review.HiddenByAdminId,
            HiddenAt = review.HiddenAt,
            IsDeleted = review.IsDeleted,
            DeletedByAdminId = review.DeletedByAdminId,
            DeletedAt = review.DeletedAt,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            HelpfulCount = review.HelpfulCount,
            ReportCount = review.ReportCount,
            CreatedAt = review.CreatedAt == default ? DateTime.UtcNow : review.CreatedAt,
            UpdatedAt = review.UpdatedAt
        };
}
