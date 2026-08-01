using System.Text.RegularExpressions;
using Fruitables.Models;
using Fruitables.Options;

namespace Fruitables.Services.Sentiment;

/// <summary>
/// Quyết định deterministic sau khi LLM đã phân tích comment.
/// LLM không được tự quyết định conflict hoặc trạng thái duyệt.
/// </summary>
public static class SentimentDecisionResolver
{
    private static readonly string[] SafetySignals =
    [
        "thối", "mốc", "ôi", "dòi", "bị sâu", "có sâu", "sâu bọ", "dị vật", "vật lạ",
        "ngộ độc", "đau bụng", "hỏng nặng", "hư hỏng", "trái cây bị hư"
    ];

    // Ghép boundary để tín hiệu chỉ khớp như một "từ" trọn vẹn. \b của .NET nhận Unicode
    // word boundary nên "ôi" không còn khớp bên trong "tôi" (IndexOf cũ gây false positive lớn).
    private static readonly Regex[] SafetySignalPatterns =
        SafetySignals
            .Select(signal => new Regex($@"\b{Regex.Escape(signal)}\b", RegexOptions.CultureInvariant))
            .ToArray();

    public static SentimentLabel FromRating(int rating) => rating switch
    {
        >= 4 => SentimentLabel.Positive,
        3 => SentimentLabel.Neutral,
        _ => SentimentLabel.Negative
    };

    public static int? SeverityFromRating(int rating) => rating switch
    {
        1 => 2,
        2 => 1,
        _ => null
    };

    public static SentimentDecision Resolve(
        int rating,
        string? comment,
        SentimentLabel commentLabel,
        int? modelSeverity,
        double? modelConfidence,
        string? reason,
        SentimentOptions options)
    {
        var ratingLabel = FromRating(rating);
        var hasSafetyRisk = HasSafetyRisk(comment);
        var effectiveCommentLabel = hasSafetyRisk ? SentimentLabel.Negative : commentLabel;
        var hasConflict = effectiveCommentLabel != SentimentLabel.Neutral
            && effectiveCommentLabel != ratingLabel;

        // Comment chỉ thay thế nhãn tổng khi comment thể hiện rõ cảm xúc.
        // Comment trung lập/mơ hồ để rating quyết định nhãn tổng.
        var effectiveLabel = effectiveCommentLabel == SentimentLabel.Neutral
            ? ratingLabel
            : effectiveCommentLabel;

        int? severity = effectiveLabel == SentimentLabel.Negative
            ? Math.Clamp(modelSeverity ?? 1, 1, 3)
            : null;

        if (hasSafetyRisk)
            severity = Math.Max(severity ?? 1, Math.Clamp(options.SafetySeverity, 1, 3));

        var confidence = modelConfidence.HasValue
            ? (float)Math.Clamp(modelConfidence.Value, 0, 1)
            : (float?)null;

        if (hasConflict && confidence.HasValue)
            confidence = Math.Min(confidence.Value, Math.Clamp(options.ConflictConfidenceCap, 0, 1));

        var finalReason = reason;
        if (hasConflict)
        {
            var direction = effectiveCommentLabel == SentimentLabel.Positive ? "tích cực" : "tiêu cực";
            finalReason = AppendReason(finalReason, $"Rating {rating} sao nhưng comment thể hiện {direction}.");
        }

        if (hasSafetyRisk)
            finalReason = AppendReason(finalReason, "Có dấu hiệu vấn đề an toàn thực phẩm.");

        // Confidence null/thấp → chưa đủ tin để tự động đưa vào KPI → bắt buộc duyệt tay.
        var lowConfidence = !confidence.HasValue || confidence.Value < options.MinConfidence;
        if (lowConfidence)
            finalReason = AppendReason(finalReason, "Độ tin cậy thấp, cần admin xác nhận.");

        var needsManualReview = (options.ManualReviewOnConflict && hasConflict)
            || hasSafetyRisk
            || lowConfidence;

        return new SentimentDecision(
            effectiveLabel,
            ratingLabel,
            effectiveCommentLabel,
            severity,
            confidence,
            hasConflict,
            needsManualReview,
            hasSafetyRisk,
            finalReason);
    }

    public static bool HasSafetyRisk(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return false;

        var text = comment.Trim().ToLowerInvariant();
        foreach (var pattern in SafetySignalPatterns)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;

            var prefix = text[..match.Index].TrimEnd();
            if (Regex.IsMatch(prefix, @"(?:^|\s)(không|chưa|chẳng)(?:\s+bị)?$", RegexOptions.CultureInvariant))
                continue;

            return true;
        }

        return false;
    }

    private static string AppendReason(string? reason, string addition)
    {
        if (string.IsNullOrWhiteSpace(reason)) return addition;
        return $"{reason.Trim().TrimEnd('.')}. {addition}";
    }
}

public sealed record SentimentDecision(
    SentimentLabel Label,
    SentimentLabel RatingSentiment,
    SentimentLabel CommentSentiment,
    int? Severity,
    float? Confidence,
    bool HasRatingCommentConflict,
    bool NeedsManualReview,
    bool HasSafetyRisk,
    string? Reason);
