using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Services.Communications;
using Fruitables.Services.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fruitables.Services.Chat.Knowledge;
using Fruitables.Services.Chat.Providers;

namespace Fruitables.Services.Sentiment;

// ============================================================
// PHÂN TÍCH CẢM XÚC REVIEW (3 mức: tích cực / trung tính / tiêu cực)
//
// Luồng:
//  - AnalyzeAsync: gom ≤ BatchSize review/call LLM (DeepSeek), parse JSON, lưu
//    ReviewSentiment + Aspects; severity >= ngưỡng → cảnh báo + SignalR.
//  - Review không có comment → suy từ rating (không gọi LLM, không tốn phí).
//  - LLM lỗi / JSON hỏng sau retry → nhãn Failed, admin bấm phân tích lại.
// ============================================================
public sealed class SentimentAnalysisService : ISentimentAnalysisService
{
    private const string ReasonNoComment = "Suy từ số sao (không có comment)";

    private readonly ApplicationDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IOutboxService _outbox;
    private readonly SentimentOptions _options;
    private readonly IRealtimeNotifier _notifier;
    private readonly IIndexingService? _indexing;
    private readonly ILogger<SentimentAnalysisService> _logger;

    public SentimentAnalysisService(
        ApplicationDbContext db,
        ILlmClient llm,
        IOutboxService outbox,
        IOptions<SentimentOptions> options,
        IRealtimeNotifier notifier,
        ILogger<SentimentAnalysisService> logger,
        IIndexingService? indexing = null)
    {
        _db = db;
        _llm = llm;
        _outbox = outbox;
        _options = options.Value;
        _notifier = notifier;
        _logger = logger;
        _indexing = indexing;
    }

    public async Task<int> AnalyzeAsync(IReadOnlyList<int> reviewIds, CancellationToken ct = default)
    {
        if (!_options.Enabled || reviewIds.Count == 0) return 0;

        var ids = reviewIds.Distinct().ToArray();
        var reviews = await _db.Reviews
            .Where(r => ids.Contains(r.Id) && !r.IsDeleted)
            .ToListAsync(ct);

        if (reviews.Count == 0) return 0;

        var withComment = reviews.Where(r => !string.IsNullOrWhiteSpace(r.Comment)).ToList();
        var noComment = reviews.Where(r => string.IsNullOrWhiteSpace(r.Comment)).ToList();

        var results = new List<SentimentResultDto>();

        // 1) Review không có chữ → fallback rating (miễn phí, không gọi LLM)
        foreach (var review in noComment)
        {
            var ratingLabel = SentimentDecisionResolver.FromRating(review.Rating);
            results.Add(new SentimentResultDto
            {
                ReviewId = review.Id,
                Label = ratingLabel,
                RatingSentiment = ratingLabel,
                CommentSentiment = null,
                Severity = SentimentDecisionResolver.SeverityFromRating(review.Rating),
                Confidence = 1.0f,
                Reason = ReasonNoComment,
                Source = SentimentSource.RatingFallback,
                AnalysisVersion = _options.AnalysisVersion
            });
        }

        // 2) Review có comment → gọi LLM theo batch
        foreach (var chunk in withComment.Chunk(Math.Max(1, _options.BatchSize)))
        {
            var chunkResults = await AnalyzeChunkWithLlmAsync(chunk, ct);
            results.AddRange(chunkResults);
        }

        await PersistAsync(results, ct);

        // Cập nhật tóm tắt cảm xúc cho chatbot (hash-skip nếu nội dung không đổi)
        if (_indexing is not null)
        {
            foreach (var productId in reviews.Select(r => r.ProductId).Distinct())
            {
                try
                {
                    await _indexing.IndexProductReviewSummaryAsync(productId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reindex review summary for product {ProductId}", productId);
                }
            }
        }

        return results.Count;
    }

    private async Task<List<SentimentResultDto>> AnalyzeChunkWithLlmAsync(IEnumerable<Review> source, CancellationToken ct)
    {
        var chunk = source.ToList();
        var systemPrompt = SentimentPromptBuilder.BuildSystemPrompt();
        var userPrompt = SentimentPromptBuilder.BuildUserPrompt(
            chunk.Select(r => (r.Id, r.Rating, r.Comment ?? string.Empty)).ToList());

        List<SentimentItemDto>? items = null;
        Exception? lastError = null;
        var infrastructureFailure = false;

        // Retry khi LLM trả rỗng / JSON hỏng (DeepSeek JSON mode thỉnh thoảng trả rỗng)
        for (var attempt = 0; attempt <= Math.Max(0, _options.RetryOnEmpty); attempt++)
        {
            try
            {
                var jsonElement = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);
                items = SentimentPromptBuilder.TryParse(jsonElement.GetRawText());
                if (items != null && items.Count > 0)
                {
                    var expectedIds = chunk.Select(r => r.Id).ToHashSet();
                    var returnedIds = items.Select(item => item.ReviewId).ToList();
                    var hasDuplicateIds = returnedIds.Count != returnedIds.Distinct().Count();
                    var hasUnknownIds = returnedIds.Any(id => !expectedIds.Contains(id));
                    var hasMissingIds = expectedIds.Any(id => !returnedIds.Contains(id));

                    if (!hasDuplicateIds && !hasUnknownIds && !hasMissingIds)
                    {
                        infrastructureFailure = false;
                        break;
                    }

                    // LLM trả về cấu trúc hợp lệ nhưng sai/thiếu review → lỗi nội dung, không phải hạ tầng.
                    lastError = new InvalidOperationException("LLM trả về danh sách review không khớp với batch yêu cầu");
                    infrastructureFailure = false;
                    items = null;
                    continue;
                }
                // Parse rỗng / không hợp lệ → provider trả rác, được retry về sau.
                lastError = new InvalidOperationException("LLM trả về JSON rỗng hoặc không có mục hợp lệ");
                infrastructureFailure = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                infrastructureFailure = true;
                _logger.LogWarning(ex, "Sentiment LLM attempt {Attempt} failed for {Count} reviews", attempt + 1, chunk.Count());
            }
        }

        // Hết retry mà vẫn không có kết quả do lỗi hạ tầng → ném ra để outbox retry/backoff
        // rồi dead-letter, thay vì đánh Failed vĩnh viễn cho cả batch vì một đợt lỗi LLM tạm thời.
        if (items is null && infrastructureFailure)
        {
            throw new SentimentTransientException(
                $"Sentiment LLM unavailable for batch of {chunk.Count} reviews after retries",
                lastError);
        }

        var parsed = items ?? new List<SentimentItemDto>();
        var results = new List<SentimentResultDto>();

        foreach (var review in chunk)
        {
            var match = parsed.FirstOrDefault(p => p.ReviewId == review.Id);
            if (match is null)
            {
                // LLM không trả review này → đánh dấu Failed (admin bấm lại được)
                results.Add(new SentimentResultDto
                {
                    ReviewId = review.Id,
                    Label = SentimentLabel.Failed,
                    RatingSentiment = SentimentDecisionResolver.FromRating(review.Rating),
                    CommentSentiment = null,
                    Confidence = null,
                    Reason = lastError is null ? "LLM không trả kết quả cho review này" : TruncateReason(lastError.Message),
                    Source = SentimentSource.AiModel,
                    NeedsManualReview = true,
                    AnalysisVersion = _options.AnalysisVersion
                });
                continue;
            }

            if (!SentimentPromptBuilder.TryMapLabel(match.Sentiment, out var label))
            {
                results.Add(new SentimentResultDto
                {
                    ReviewId = review.Id,
                    Label = SentimentLabel.Failed,
                    RatingSentiment = SentimentDecisionResolver.FromRating(review.Rating),
                    CommentSentiment = null,
                    Confidence = null,
                    Reason = "Nhãn LLM trả về không hợp lệ",
                    Source = SentimentSource.AiModel,
                    NeedsManualReview = true,
                    AnalysisVersion = _options.AnalysisVersion
                });
                continue;
            }

            var decision = SentimentDecisionResolver.Resolve(
                review.Rating,
                review.Comment,
                label,
                match.Severity,
                match.Confidence,
                match.Reason,
                _options);

            var aspects = new List<SentimentAspectDto>();
            foreach (var item in match.Aspects)
            {
                if (!SentimentPromptBuilder.TryMapAspect(item.Aspect, out var aspect)
                    || !SentimentPromptBuilder.TryMapLabel(item.Sentiment, out var aspectLabel))
                    continue;

                aspects.Add(new SentimentAspectDto
                {
                    Aspect = aspect.ToString(),
                    Sentiment = aspectLabel.ToString(),
                    Severity = aspectLabel == SentimentLabel.Negative
                        ? Math.Clamp(item.Severity ?? 1, 1, 3)
                        : null
                });
            }

            results.Add(new SentimentResultDto
            {
                ReviewId = review.Id,
                Label = decision.Label,
                RatingSentiment = decision.RatingSentiment,
                CommentSentiment = decision.CommentSentiment,
                Severity = decision.Severity,
                Confidence = decision.Confidence,
                Reason = TruncateReason(decision.Reason),
                Source = SentimentSource.AiModel,
                HasRatingCommentConflict = decision.HasRatingCommentConflict,
                NeedsManualReview = decision.NeedsManualReview,
                HasSafetyRisk = decision.HasSafetyRisk,
                AnalysisVersion = _options.AnalysisVersion,
                Aspects = aspects
            });
        }

        return results;
    }

    private async Task PersistAsync(IReadOnlyList<SentimentResultDto> results, CancellationToken ct)
    {
        var reviewIds = results.Select(r => r.ReviewId).ToArray();
        var existing = await _db.ReviewSentiments
            .Where(s => reviewIds.Contains(s.ReviewId))
            .Include(s => s.Aspects)
            .ToDictionaryAsync(s => s.ReviewId, ct);

        var newlyAlerted = new List<(int ReviewId, string ProductName, string Snippet)>();

        foreach (var result in results)
        {
            if (!existing.TryGetValue(result.ReviewId, out var sentiment))
            {
                sentiment = new ReviewSentiment { ReviewId = result.ReviewId };
                _db.ReviewSentiments.Add(sentiment);
            }
            else
            {
                if (sentiment.Source == SentimentSource.AdminOverride)
                    continue;

                _db.ReviewSentimentAspects.RemoveRange(sentiment.Aspects);
                sentiment.Aspects.Clear();
            }

            var wasAlerted = sentiment.AlertStatus != SentimentAlertStatus.None;
            var alertNow = result.HasSafetyRisk
                || (result.Label == SentimentLabel.Negative
                    && result.Severity.HasValue
                    && result.Severity.Value >= _options.SevereThreshold);

            sentiment.Sentiment = result.Label;
            sentiment.RatingSentiment = result.RatingSentiment;
            sentiment.CommentSentiment = result.CommentSentiment;
            sentiment.HasRatingCommentConflict = result.HasRatingCommentConflict;
            sentiment.NeedsManualReview = result.NeedsManualReview;
            sentiment.HasSafetyRisk = result.HasSafetyRisk;
            sentiment.Severity = result.Severity;
            sentiment.Confidence = result.Confidence;
            sentiment.Reason = result.Reason;
            sentiment.Source = result.Source;
            sentiment.AnalyzedAtUtc = DateTime.UtcNow;
            sentiment.AnalysisVersion = result.AnalysisVersion ?? _options.AnalysisVersion;

            if (alertNow)
            {
                if (!wasAlerted)
                {
                    sentiment.AlertStatus = SentimentAlertStatus.Pending;
                    var review = await _db.Reviews.AsNoTracking()
                        .Include(r => r.Product)
                        .FirstOrDefaultAsync(r => r.Id == result.ReviewId, ct);
                    newlyAlerted.Add((
                        result.ReviewId,
                        review?.Product?.Name ?? $"Sản phẩm #{review?.ProductId}",
                        SentimentPromptBuilder.Truncate(review?.Comment, 80)));
                }
                // alertNow && wasAlerted → giữ nguyên trạng thái (Pending/Acknowledged), không reset.
            }
            else if (wasAlerted)
            {
                // Không còn đạt ngưỡng cảnh báo (severity giảm xuống dưới ngưỡng, hết tiêu cực,
                // hoặc hết dấu hiệu an toàn) → gỡ cảnh báo và xóa metadata xác nhận cũ.
                sentiment.AlertStatus = SentimentAlertStatus.None;
                sentiment.AcknowledgedById = null;
                sentiment.AcknowledgedAtUtc = null;
            }

            foreach (var aspect in result.Aspects)
            {
                if (!Enum.TryParse<SentimentAspect>(aspect.Aspect, out var aspectEnum)) continue;
                if (!Enum.TryParse<SentimentLabel>(aspect.Sentiment, out var aspectLabel)) continue;

                sentiment.Aspects.Add(new ReviewSentimentAspect
                {
                    Aspect = aspectEnum,
                    Sentiment = aspectLabel,
                    Severity = aspect.Severity
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Cảnh báo realtime tới group Admins (chỉ 1 lần khi chuyển sang Pending)
        foreach (var (reviewId, productName, snippet) in newlyAlerted)
        {
            try
            {
                await _notifier.NotifySevereReviewAlertAsync(reviewId, productName, snippet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying severe review alert for review {ReviewId}", reviewId);
            }
        }
    }

    public async Task<int> CountUnanalyzedAsync(CancellationToken ct = default)
        => await _db.Reviews
            .CountAsync(r => !r.IsDeleted
                && !_db.ReviewSentiments.Any(s => s.ReviewId == r.Id
                    && (s.Source == SentimentSource.AdminOverride
                        || (s.Sentiment != SentimentLabel.Failed && s.AnalysisVersion == _options.AnalysisVersion))), ct);

    public async Task<int> EnqueueBackfillAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return 0;

        var ids = await _db.Reviews
            .Where(r => !r.IsDeleted
                && !_db.ReviewSentiments.Any(s => s.ReviewId == r.Id
                    && (s.Source == SentimentSource.AdminOverride
                        || (s.Sentiment != SentimentLabel.Failed && s.AnalysisVersion == _options.AnalysisVersion))))
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var chunks = ids.Chunk(Math.Max(1, _options.BackfillChunkSize)).ToList();

        // Key theo đợt (batchId) — key tĩnh theo review sẽ chặn các đợt backfill sau
        // vì message cũ đã xử lý vẫn giữ idempotency key.
        var batchId = Guid.NewGuid().ToString("N")[..8];
        foreach (var chunk in chunks)
        {
            var key = $"sentiment-backfill-{batchId}-{chunk[0]}-{chunk[^1]}";
            await _outbox.EnqueueAsync(OutboxMessageTypes.ReviewSentimentBackfill, new { ReviewIds = chunk }, key, ct);
        }
        await _db.SaveChangesAsync(ct);
        return chunks.Count;
    }

    public async Task<SentimentDashboardData> GetDashboardAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-13);

        // Quy tắc eligibility nhất quán cho KPI vận hành: review không bị xóa, không bị ẩn,
        // không chờ duyệt tay và không Failed.
        var baseQuery =
            from r in _db.Reviews
            join s in _db.ReviewSentiments on r.Id equals s.ReviewId
            where !r.IsDeleted && !r.IsHidden
            select new { r, s };

        var eligibleQuery = baseQuery.Where(x => !x.s.NeedsManualReview && x.s.Sentiment != SentimentLabel.Failed);

        // Phân bố cảm xúc tính phía server (không tải toàn bộ lịch sử vào RAM rồi đếm).
        var distribution = await eligibleQuery
            .GroupBy(x => x.s.Sentiment)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(SentimentLabel label) => distribution.FirstOrDefault(d => d.Label == label)?.Count ?? 0;
        var positiveCount = CountOf(SentimentLabel.Positive);
        var neutralCount = CountOf(SentimentLabel.Neutral);
        var negativeCount = CountOf(SentimentLabel.Negative);

        var data = new SentimentDashboardData
        {
            TotalAnalyzed = positiveCount + neutralCount + negativeCount,
            PositiveCount = positiveCount,
            NeutralCount = neutralCount,
            NegativeCount = negativeCount,
            FailedCount = await baseQuery.CountAsync(x => x.s.Sentiment == SentimentLabel.Failed, ct),
            PendingAlertCount = await baseQuery.CountAsync(x => x.s.AlertStatus == SentimentAlertStatus.Pending, ct),
            PendingReviewCount = await baseQuery.CountAsync(x => x.s.NeedsManualReview, ct),
            ConflictCount = await baseQuery.CountAsync(x => x.s.HasRatingCommentConflict, ct),
            SafetyRiskCount = await baseQuery.CountAsync(x => x.s.HasSafetyRisk, ct),
            UnanalyzedCount = await CountUnanalyzedAsync(ct)
        };
        data.NegativeRate = data.TotalAnalyzed == 0 ? 0 : (float)Math.Round(data.NegativeCount * 100f / data.TotalAnalyzed, 1);

        // Xu hướng 14 ngày: lọc cửa sổ 14 ngày phía server rồi mới group trong memory,
        // thay vì materialize toàn bộ lịch sử.
        var trendRows = await eligibleQuery
            .Where(x => x.r.CreatedAt >= since)
            .Select(x => new { x.r.CreatedAt, x.s.Sentiment })
            .ToListAsync(ct);

        var days = new List<SentimentTrendPoint>();
        for (var d = 0; d <= 13; d++)
        {
            var date = DateTime.UtcNow.Date.AddDays(-d);
            days.Add(new SentimentTrendPoint
            {
                Date = date.ToString("dd/MM"),
                Positive = trendRows.Count(x => x.CreatedAt.Date == date && x.Sentiment == SentimentLabel.Positive),
                Neutral = trendRows.Count(x => x.CreatedAt.Date == date && x.Sentiment == SentimentLabel.Neutral),
                Negative = trendRows.Count(x => x.CreatedAt.Date == date && x.Sentiment == SentimentLabel.Negative)
            });
        }
        days.Reverse();
        data.Trend = days;

        // Top khía cạnh bị chê (aspect negative) — join sang Review để loại review ẩn/xóa nhất quán.
        data.TopNegativeAspects = await (
            from a in _db.ReviewSentimentAspects
            join s in _db.ReviewSentiments on a.ReviewSentimentId equals s.Id
            join r in _db.Reviews on s.ReviewId equals r.Id
            where !r.IsDeleted && !r.IsHidden && !s.NeedsManualReview && a.Sentiment == SentimentLabel.Negative
            group a by a.Aspect into g
            select new AspectCount { Aspect = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(a => a.Count)
            .Take(5)
            .ToListAsync(ct);

        // Top sản phẩm bị chê (review negative, không ẩn)
        data.TopNegativeProducts = await (
            from r in _db.Reviews
            join s in _db.ReviewSentiments on r.Id equals s.ReviewId
            where !r.IsDeleted && !r.IsHidden && !s.NeedsManualReview
            group new { r, s } by new { r.ProductId, r.Product.Name } into g
            select new ProductSentiment
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                NegativeCount = g.Count(x => x.s.Sentiment == SentimentLabel.Negative),
                ConflictCount = g.Count(x => x.s.HasRatingCommentConflict),
                TotalCount = g.Count()
            })
            .OrderByDescending(p => p.NegativeCount)
            .ThenByDescending(p => p.TotalCount)
            .Take(5)
            .ToListAsync(ct);

        foreach (var p in data.TopNegativeProducts)
            p.NegativeRate = p.TotalCount == 0 ? 0 : (float)Math.Round(p.NegativeCount * 100f / p.TotalCount, 1);

        return data;
    }

    public async Task<PagedSentimentReviews> GetReviewsAsync(SentimentReviewFilter filter, int maxPageSize = 100, CancellationToken ct = default)
    {
        var query = _db.Reviews
            .Where(r => !r.IsDeleted)
            .Join(_db.ReviewSentiments, r => r.Id, s => s.ReviewId, (r, s) => new { r, s });

        if (filter.ProductId.HasValue) query = query.Where(x => x.r.ProductId == filter.ProductId.Value);
        if (filter.Sentiment.HasValue) query = query.Where(x => x.s.Sentiment == filter.Sentiment.Value);
        if (filter.Severity.HasValue) query = query.Where(x => x.s.Severity == filter.Severity.Value);
        if (filter.AlertOnly == true) query = query.Where(x => x.s.AlertStatus == SentimentAlertStatus.Pending);
        if (filter.ConflictOnly == true) query = query.Where(x => x.s.HasRatingCommentConflict);
        if (filter.NeedsManualReviewOnly == true) query = query.Where(x => x.s.NeedsManualReview);
        if (filter.SafetyOnly == true) query = query.Where(x => x.s.HasSafetyRisk);
        if (filter.From.HasValue) query = query.Where(x => x.r.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(x => x.r.CreatedAt <= filter.To.Value);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, Math.Max(1, maxPageSize));
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.r.Id,
                SentimentId = x.s.Id,
                x.r.ProductId,
                ProductName = x.r.Product.Name,
                UserName = x.r.User.Name,
                x.r.Rating,
                x.r.Comment,
                x.r.CreatedAt,
                 x.r.IsVerifiedPurchase,
                 x.s.Sentiment,
                 x.s.RatingSentiment,
                 x.s.CommentSentiment,
                 x.s.HasRatingCommentConflict,
                 x.s.NeedsManualReview,
                 x.s.HasSafetyRisk,
                 x.s.Severity,
                x.s.Confidence,
                x.s.Reason,
                 x.s.Source,
                 x.s.AnalyzedAtUtc,
                 x.s.AnalysisVersion,
                 x.s.AlertStatus
            })
            .ToListAsync(ct);

        var sentimentIds = items.Select(i => i.SentimentId).ToArray();
        var aspects = await _db.ReviewSentimentAspects
            .Where(a => sentimentIds.Contains(a.ReviewSentimentId))
            .Join(_db.ReviewSentiments, a => a.ReviewSentimentId, s => s.Id, (a, s) => new { a, s.ReviewId })
            .ToListAsync(ct);

        var rows = items.Select(i => new SentimentReviewRow
        {
            ReviewId = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName ?? $"Sản phẩm #{i.ProductId}",
            UserName = i.UserName ?? "Khách",
            Rating = i.Rating,
            Comment = i.Comment ?? string.Empty,
            CreatedAt = i.CreatedAt,
            IsVerifiedPurchase = i.IsVerifiedPurchase,
            Label = i.Sentiment,
            RatingSentiment = i.RatingSentiment,
            CommentSentiment = i.CommentSentiment,
            HasRatingCommentConflict = i.HasRatingCommentConflict,
            NeedsManualReview = i.NeedsManualReview,
            HasSafetyRisk = i.HasSafetyRisk,
            Severity = i.Severity,
            Confidence = i.Confidence,
            Reason = i.Reason,
            Source = i.Source,
            AnalyzedAtUtc = i.AnalyzedAtUtc,
            AnalysisVersion = i.AnalysisVersion,
            AlertStatus = i.AlertStatus,
            Aspects = aspects
                .Where(a => a.ReviewId == i.Id)
                .Select(a => new SentimentAspectDto
                {
                    Aspect = a.a.Aspect.ToString(),
                    Sentiment = a.a.Sentiment.ToString(),
                    Severity = a.a.Severity
                })
                .ToList()
        }).ToList();

        return new PagedSentimentReviews
        {
            Items = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<bool> OverrideAsync(int reviewId, SentimentLabel label, int? severity, string? note, int adminId, CancellationToken ct = default)
    {
        var sentiment = await _db.ReviewSentiments.FirstOrDefaultAsync(s => s.ReviewId == reviewId, ct);
        if (sentiment is null) return false;
        if ((sentiment.HasRatingCommentConflict || sentiment.HasSafetyRisk)
            && string.IsNullOrWhiteSpace(note))
            return false;

        sentiment.Sentiment = label;
        sentiment.Severity = label == SentimentLabel.Negative && severity.HasValue ? Math.Clamp(severity.Value, 1, 3) : null;
        sentiment.Source = SentimentSource.AdminOverride;
        sentiment.NeedsManualReview = false;
        sentiment.AdminOverrideById = adminId;
        sentiment.AdminOverrideAtUtc = DateTime.UtcNow;
        sentiment.AdminReviewNote = TruncateReason(note, 400);

        // Vòng đời cảnh báo phải phản ánh nhãn admin vừa gán (đối xứng với PersistAsync).
        var alertNow = sentiment.HasSafetyRisk
            || (label == SentimentLabel.Negative
                && sentiment.Severity.HasValue
                && sentiment.Severity.Value >= _options.SevereThreshold);
        var wasAlerted = sentiment.AlertStatus != SentimentAlertStatus.None;
        var newlyAlerted = false;

        if (alertNow && !wasAlerted)
        {
            sentiment.AlertStatus = SentimentAlertStatus.Pending;
            newlyAlerted = true;
        }
        else if (!alertNow && wasAlerted)
        {
            sentiment.AlertStatus = SentimentAlertStatus.None;
            sentiment.AcknowledgedById = null;
            sentiment.AcknowledgedAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);

        if (newlyAlerted)
        {
            try
            {
                var review = await _db.Reviews.AsNoTracking()
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.Id == reviewId, ct);
                await _notifier.NotifySevereReviewAlertAsync(
                    reviewId,
                    review?.Product?.Name ?? $"Sản phẩm #{review?.ProductId}",
                    SentimentPromptBuilder.Truncate(review?.Comment, 80));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying severe review alert for review {ReviewId}", reviewId);
            }
        }

        return true;
    }

    public async Task<bool> AcknowledgeAlertAsync(int reviewId, int adminId, CancellationToken ct = default)
    {
        var sentiment = await _db.ReviewSentiments.FirstOrDefaultAsync(s => s.ReviewId == reviewId, ct);
        if (sentiment is null || sentiment.AlertStatus != SentimentAlertStatus.Pending) return false;

        sentiment.AlertStatus = SentimentAlertStatus.Acknowledged;
        sentiment.AcknowledgedById = adminId;
        sentiment.AcknowledgedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CountPendingAlertsAsync(CancellationToken ct = default)
        => await _db.ReviewSentiments.CountAsync(s => s.AlertStatus == SentimentAlertStatus.Pending, ct);

    public async Task<ReviewContextDto?> GetReviewContextAsync(int reviewId, CancellationToken ct = default)
    {
        var review = await _db.Reviews.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, ct);
        if (review is null) return null;

        // Đơn hàng gần nhất chưa hủy của khách chứa sản phẩm này
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.UserId == review.UserId
                && o.Status != OrderStatus.Cancelled
                && o.Items.Any(i => i.ProductId == review.ProductId))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new ReviewContextDto
        {
            ReviewId = review.Id,
            UserId = review.UserId,
            UserName = review.User?.Name ?? "Khách hàng",
            UserEmail = review.User?.Email ?? string.Empty,
            ProductId = review.ProductId,
            ProductName = review.Product?.Name ?? $"Sản phẩm #{review.ProductId}",
            OrderId = order?.Id,
            OrderNumber = order?.OrderNumber
        };
    }

    public async Task<string?> GenerateReplyDraftAsync(int reviewId, CancellationToken ct = default)
    {
        var review = await _db.Reviews.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, ct);
        if (review is null) return null;

        var sentiment = await _db.ReviewSentiments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ReviewId == reviewId, ct);
        if (sentiment is null
            || sentiment.NeedsManualReview
            || (sentiment.CommentSentiment != SentimentLabel.Negative
                && sentiment.Sentiment != SentimentLabel.Negative))
            return null;

        var system = """
            Bạn là nhân viên chăm sóc khách hàng của cửa hàng trái cây / thực phẩm tươi Fruitables (Việt Nam).
            Viết email phản hồi cho khách sau đánh giá tiêu cực. Yêu cầu:
            - Ngắn gọn (tối đa 120 từ), tiếng Việt, lịch sự, chân thành, không máy móc.
            - Xin lỗi về trải nghiệm không tốt, nêu đúng vấn đề khách gặp phải (không bịa nguyên nhân).
            - Hứa sẽ kiểm tra / cải thiện; nếu khách cần hỗ trợ thêm thì hướng dẫn liên hệ (không bịa số điện thoại cụ thể).
            - Không hứa đền bù bằng số tiền cụ thể nếu chưa được phép.
            - Bắt đầu bằng "Xin chào <tên khách>," và kết bằng "Trân trọng, Đội ngũ Fruitables".
            """;

        var user = $"Khách hàng: {review.User?.Name}\nSản phẩm: {review.Product?.Name}\nĐánh giá {review.Rating}/5 sao.\nNội dung phàn nàn: {SentimentPromptBuilder.Truncate(review.Comment, 600)}\n\nViết email phản hồi (không gửi kèm tiêu đề):";

        try
        {
            var reply = await _llm.CompleteAsync(system, user, ct);
            return string.IsNullOrWhiteSpace(reply) ? null : reply.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate reply draft for review {ReviewId}", reviewId);
            return null;
        }
    }

    private static string? TruncateReason(string? reason, int max = 120)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        return reason.Length <= max ? reason : reason[..max];
    }
}
