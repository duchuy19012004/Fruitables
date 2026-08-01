using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Options;
using Fruitables.Repositories;
using Fruitables.Services;
using Fruitables.Services.Chat;
using Fruitables.Services.Interfaces;
using Fruitables.Services.Outbox;
using Fruitables.Services.Sentiment;
using Fruitables.Tests.Chat.Fakes;
using Fruitables.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fruitables.Tests;

internal static class SentimentTestExtensions
{
    public static IOptions<SentimentOptions> AsOptions(this SentimentOptions options) => Microsoft.Extensions.Options.Options.Create(options);
}

public class SentimentModuleTests
{
    private static ApplicationDbContext CreateContext() => new(TestDbContextFactory.CreateSqliteOptions());

    private static SentimentOptions Options() => new()
    {
        Enabled = true,
        BatchSize = 15,
        BackfillChunkSize = 15,
        SevereThreshold = 3,
        RetryOnEmpty = 2
    };

    private static SentimentAnalysisService CreateService(
        ApplicationDbContext db,
        ILlmClient llm,
        Mock<IRealtimeNotifier>? notifier = null,
        IOutboxService? outbox = null)
        => new(
            db,
            llm,
            outbox ?? new OutboxService(db, TimeProvider.System),
            Options().AsOptions(),
            (notifier ?? new Mock<IRealtimeNotifier>()).Object,
            NullLogger<SentimentAnalysisService>.Instance);

    private static (User User, Category Category, Product Product) SeedCatalog(ApplicationDbContext db)
    {
        var user = new User { Name = "Nguyễn Văn A", Email = "a@test.com", Password = "x" };
        var category = new Category { Name = "Trái cây", Slug = "trai-cay" };
        db.Users.Add(user);
        db.Categories.Add(category);
        db.SaveChanges();
        var product = new Product
        {
            Name = "Táo Fuji",
            Slug = "tao-fuji",
            CategoryId = category.Id,
            Price = 100000m,
            DisplayMinPrice = 100000m,
            DisplayMaxPrice = 100000m
        };
        db.Products.Add(product);
        db.SaveChanges();
        return (user, category, product);
    }

    private static Review AddReview(ApplicationDbContext db, int productId, int userId, int rating, string? comment, DateTime? createdAt = null)
    {
        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.Reviews.Add(review);
        db.SaveChanges();
        return review;
    }

    // ============ Prompt builder + parser ============

    [Fact]
    public void SystemPrompt_ContainsJsonWordAndExample_RequiredByDeepSeekJsonMode()
    {
        var prompt = SentimentPromptBuilder.BuildSystemPrompt();
        Assert.Contains("json", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"results\"", prompt);
        Assert.Contains("\"reviewId\"", prompt);
    }

    [Fact]
    public void SystemPrompt_SeparatesCommentSentimentFromRating_WhenConflict()
    {
        var prompt = SentimentPromptBuilder.BuildSystemPrompt();
        Assert.Contains("sentiment\" là cảm xúc của RIÊNG NỘI DUNG COMMENT", prompt);
        Assert.Contains("Số sao chỉ là dữ liệu đối chiếu", prompt);
        Assert.Contains("trái cây bị hư", prompt);       // 5 sao + comment chê → negative
        Assert.Contains("sản phẩm này tốt", prompt);     // 1 sao + comment khen → positive
        Assert.Contains("Không suy đoán nguyên nhân", prompt);
        Assert.Contains("dữ liệu không đáng tin cậy", prompt);
    }

    [Fact]
    public void Resolver_ConflictingRatingAndComment_RequiresManualReview()
    {
        var decision = SentimentDecisionResolver.Resolve(
            1,
            "sản phẩm này tốt",
            SentimentLabel.Positive,
            null,
            0.95,
            "Comment khen sản phẩm",
            Options());

        Assert.Equal(SentimentLabel.Negative, decision.RatingSentiment);
        Assert.Equal(SentimentLabel.Positive, decision.CommentSentiment);
        Assert.Equal(SentimentLabel.Positive, decision.Label);
        Assert.True(decision.HasRatingCommentConflict);
        Assert.True(decision.NeedsManualReview);
        Assert.Contains("Rating 1 sao", decision.Reason);
    }

    [Fact]
    public void Resolver_FoodSafetyComment_ForcesSevereAlertRegardlessOfRating()
    {
        var decision = SentimentDecisionResolver.Resolve(
            5,
            "trái cây bị hư",
            SentimentLabel.Negative,
            2,
            0.95,
            "Khách phản ánh trái cây bị hư",
            Options());

        Assert.Equal(SentimentLabel.Positive, decision.RatingSentiment);
        Assert.Equal(SentimentLabel.Negative, decision.CommentSentiment);
        Assert.Equal(SentimentLabel.Negative, decision.Label);
        Assert.True(decision.HasRatingCommentConflict);
        Assert.True(decision.HasSafetyRisk);
        Assert.True(decision.NeedsManualReview);
        Assert.Equal(3, decision.Severity);
    }

    [Fact]
    public void Resolver_DoesNotFlagNegatedSafetyPhrase()
    {
        Assert.False(SentimentDecisionResolver.HasSafetyRisk("Trái cây không bị hư, chỉ giao hơi trễ."));
        Assert.True(SentimentDecisionResolver.HasSafetyRisk("Trái cây không bị hư nhưng có mốc."));
    }

    [Fact]
    public async Task AnalyzeAsync_ConflictingRatingAndComment_UsesLlmLabel()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);

        // 5 sao nhưng comment chê — LLM (theo rule comment thắng) trả negative
        var fiveStarNegative = AddReview(db, product.Id, user.Id, 5, "trái cây bị hư");
        var oneStarPositive = AddReview(db, product.Id, user.Id, 1, "sản phẩm này tốt");

        var llm = new FakeLlmClient
        {
            JsonResponse = """
                {"results":[
                  {"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.95,"reason":"Sao 5 sao nhưng comment chê: trái cây bị hư","aspects":[{"aspect":"quality","sentiment":"negative","severity":3}]},
                  {"reviewId":2,"sentiment":"positive","confidence":0.9,"reason":"Sao 1 sao nhưng comment khen","aspects":[]}
                ]}
                """
        };
        var service = CreateService(db, llm);

        await service.AnalyzeAsync(new[] { fiveStarNegative.Id, oneStarPositive.Id });

        var neg = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == fiveStarNegative.Id);
        var pos = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == oneStarPositive.Id);
        Assert.Equal(SentimentLabel.Negative, neg.Sentiment);
        Assert.Equal(3, neg.Severity);
        Assert.True(neg.HasRatingCommentConflict);
        Assert.True(neg.NeedsManualReview);
        Assert.True(neg.HasSafetyRisk);
        Assert.Equal(SentimentLabel.Positive, pos.Sentiment);
        Assert.True(pos.HasRatingCommentConflict);
        Assert.True(pos.NeedsManualReview);
    }

    [Fact]
    public async Task OverrideAsync_ConflictRequiresNoteAndClearsManualReview()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 5, "Trái cây bị hư");
        var admin = new User { Name = "Admin", Email = "conflict-admin@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":2,"confidence":0.9,"reason":"trái cây bị hư","aspects":[] }]}"""
        };
        var service = CreateService(db, llm);
        await service.AnalyzeAsync(new[] { review.Id });

        Assert.False(await service.OverrideAsync(review.Id, SentimentLabel.Negative, 3, null, admin.Id));
        Assert.True(await service.OverrideAsync(review.Id, SentimentLabel.Negative, 3, "Đã xác minh với khách", admin.Id));

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.False(sentiment.NeedsManualReview);
        Assert.True(sentiment.HasRatingCommentConflict);
        Assert.Equal(SentimentSource.AdminOverride, sentiment.Source);
    }

    [Fact]
    public void TryParse_ParsesArrayResponse()
    {
        const string json = """
            {"results":[
              {"reviewId":1,"sentiment":"positive","severity":null,"confidence":0.9,"reason":"ngon","aspects":[]},
              {"reviewId":2,"sentiment":"negative","severity":3,"confidence":0.95,"reason":"hỏng","aspects":[{"aspect":"quality","sentiment":"negative","severity":3}]}
            ]}
            """;

        var items = SentimentPromptBuilder.TryParse(json);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal("positive", items[0].Sentiment);
        Assert.Equal(3, items[1].Severity);
        Assert.Single(items[1].Aspects);
        Assert.Equal("quality", items[1].Aspects[0].Aspect);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("""{"results":[]}""")]
    public void TryParse_ReturnsNullOnInvalid(string json)
    {
        Assert.Null(SentimentPromptBuilder.TryParse(json));
    }

    [Theory]
    [InlineData("positive", SentimentLabel.Positive)]
    [InlineData("neutral", SentimentLabel.Neutral)]
    [InlineData("negative", SentimentLabel.Negative)]
    [InlineData("NEGATIVE", SentimentLabel.Negative)]
    [InlineData("banana", SentimentLabel.Failed)]
    public void TryMapLabel_MapsOnlyThreeLevels(string label, SentimentLabel expected)
    {
        Assert.Equal(expected, SentimentPromptBuilder.TryMapLabel(label, out var result) ? result : SentimentLabel.Failed);
    }

    // ============ Fallback rating (review không có chữ) ============

    [Theory]
    [InlineData(5, SentimentLabel.Positive)]
    [InlineData(4, SentimentLabel.Positive)]
    [InlineData(3, SentimentLabel.Neutral)]
    [InlineData(2, SentimentLabel.Negative)]
    [InlineData(1, SentimentLabel.Negative)]
    public async Task AnalyzeAsync_NoCommentReview_FallsBackFromRating_WithoutCallingLlm(int rating, SentimentLabel expected)
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, rating, null);

        var llm = new FakeLlmClient();
        var service = CreateService(db, llm);

        var count = await service.AnalyzeAsync(new[] { review.Id });

        Assert.Equal(1, count);
        Assert.Empty(llm.Calls);
        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(expected, sentiment.Sentiment);
        Assert.Equal(SentimentSource.RatingFallback, sentiment.Source);
        Assert.Equal(1f, sentiment.Confidence);
    }

    // ============ LLM batch ============

    [Fact]
    public async Task AnalyzeAsync_WithComment_UsesLlmAndSavesAspects()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 2, "Giao hàng trễ, táo dập hỏng");

        var llm = new FakeLlmClient
        {
            JsonResponse = """
                {"results":[
                  {"reviewId":1,"sentiment":"negative","severity":2,"confidence":0.9,"reason":"giao trễ, táo dập","aspects":[
                    {"aspect":"delivery","sentiment":"negative","severity":1},
                    {"aspect":"quality","sentiment":"negative","severity":2}]}
                ]}
                """
        };
        var service = CreateService(db, llm);

        var count = await service.AnalyzeAsync(new[] { review.Id });

        Assert.Equal(1, count);
        Assert.Single(llm.Calls);
        var sentiment = await db.ReviewSentiments.Include(s => s.Aspects).SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentLabel.Negative, sentiment.Sentiment);
        Assert.Equal(2, sentiment.Severity);
        Assert.Equal(SentimentSource.AiModel, sentiment.Source);
        Assert.Equal(2, sentiment.Aspects.Count);
        Assert.Equal(SentimentAspect.Delivery, sentiment.Aspects.First().Aspect);
        Assert.Equal(SentimentAlertStatus.None, sentiment.AlertStatus); // severity 2 < ngưỡng 3
    }

    [Fact]
    public async Task AnalyzeAsync_SevereNegative_CreatesAlertAndNotifiesAdmins()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 1, "Táo thối nát hoàn toàn, đòi hoàn tiền!");

        var llm = new FakeLlmClient
        {
            JsonResponse = """
                {"results":[{"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.98,"reason":"hàng hỏng nặng","aspects":[{"aspect":"quality","sentiment":"negative","severity":3}]}]}
                """
        };
        var notifier = new Mock<IRealtimeNotifier>();
        var service = CreateService(db, llm, notifier);

        await service.AnalyzeAsync(new[] { review.Id });

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentAlertStatus.Pending, sentiment.AlertStatus);
        notifier.Verify(n => n.NotifySevereReviewAlertAsync(review.Id, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_Reanalysis_DoesNotResetAcknowledgedAlert()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 1, "Hỏng nặng!");
        var admin = new User { Name = "Admin", Email = "admin@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var service = CreateService(db, llm);

        await service.AnalyzeAsync(new[] { review.Id });
        await service.AcknowledgeAlertAsync(review.Id, admin.Id);

        // Phân tích lại vẫn tiêu cực mức 3 → không reset trạng thái đã xác nhận
        await service.AnalyzeAsync(new[] { review.Id });

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentAlertStatus.Acknowledged, sentiment.AlertStatus);
    }

    [Fact]
    public async Task AnalyzeAsync_LlmProviderFailure_ThrowsTransient_DoesNotPersistFailed()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 4, "Comment có chữ nhưng LLM lỗi");

        var llm = new FakeLlmClient { JsonResponse = "not-json-at-all" };
        var service = CreateService(db, llm);

        // Lỗi hạ tầng (JSON hỏng từ provider) sau retry → ném transient để outbox retry/dead-letter,
        // KHÔNG đánh Failed vĩnh viễn và KHÔNG lưu gì.
        await Assert.ThrowsAsync<SentimentTransientException>(() => service.AnalyzeAsync(new[] { review.Id }));
        Assert.Equal(3, llm.Calls.Count); // retry hết 2 lần + lần đầu
        Assert.Empty(await db.ReviewSentiments.ToListAsync());
    }

    [Fact]
    public async Task AnalyzeAsync_LlmReturnsMismatchedReviewIds_MarksFailed_WithoutThrowing()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var r1 = AddReview(db, product.Id, user.Id, 5, "Ngon");
        var r2 = AddReview(db, product.Id, user.Id, 4, "Ổn áp");

        // LLM thiếu r2 → danh sách không khớp → lỗi NỘI DUNG (không phải hạ tầng) → Failed, không throw.
        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"positive","confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var service = CreateService(db, llm);

        await service.AnalyzeAsync(new[] { r1.Id, r2.Id }); // không throw

        Assert.Equal(2, await db.ReviewSentiments.CountAsync(s => s.Sentiment == SentimentLabel.Failed));
        Assert.Equal(2, await db.ReviewSentiments.CountAsync(s => s.NeedsManualReview));
    }

    [Fact]
    public async Task AnalyzeAsync_LlmSucceedsOnRetry_AfterEmptyResponse()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 5, "Rất ngon, sẽ mua lại");

        var llm = new FlakyLlmClient(
            failures: 1,
            success: """{"results":[{"reviewId":1,"sentiment":"positive","confidence":0.9,"reason":"ngon","aspects":[]}]}""");
        var service = CreateService(db, llm);

        await service.AnalyzeAsync(new[] { review.Id });

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentLabel.Positive, sentiment.Sentiment);
        Assert.Equal(2, llm.Calls.Count);
    }

    // ============ Batch gom nhiều review ============

    [Fact]
    public async Task AnalyzeAsync_MultipleReviews_BatchedInOneLlmCall()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var r1 = AddReview(db, product.Id, user.Id, 5, "Ngon quá");
        var r2 = AddReview(db, product.Id, user.Id, 4, "Ổn");
        var r3 = AddReview(db, product.Id, user.Id, 2, "Không hài lòng");

        var llm = new FakeLlmClient
        {
            JsonResponse = """
                {"results":[
                  {"reviewId":1,"sentiment":"positive","confidence":0.9,"reason":"x","aspects":[]},
                  {"reviewId":2,"sentiment":"positive","confidence":0.8,"reason":"x","aspects":[]},
                  {"reviewId":3,"sentiment":"negative","severity":2,"confidence":0.9,"reason":"x","aspects":[]}
                ]}
                """
        };
        var service = CreateService(db, llm);

        var count = await service.AnalyzeAsync(new[] { r1.Id, r2.Id, r3.Id });

        Assert.Equal(3, count);
        Assert.Single(llm.Calls);
        Assert.Equal(3, await db.ReviewSentiments.CountAsync());
    }

    // ============ Override + Acknowledge ============

    [Fact]
    public async Task OverrideAsync_ChangesLabelAndSource_ClearsAlertWhenNonNegative()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 1, "Hỏng");
        var admin = new User { Name = "Admin", Email = "admin2@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var service = CreateService(db, llm);
        await service.AnalyzeAsync(new[] { review.Id });

        var ok = await service.OverrideAsync(review.Id, SentimentLabel.Positive, null, "Khách quên dấu - thực tế khen", admin.Id);

        Assert.True(ok);
        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentLabel.Positive, sentiment.Sentiment);
        Assert.Equal(SentimentSource.AdminOverride, sentiment.Source);
        Assert.Equal(admin.Id, sentiment.AdminOverrideById);
        Assert.Equal(SentimentAlertStatus.None, sentiment.AlertStatus);
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_MarksAcknowledged()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 1, "Tệ");
        var admin = new User { Name = "Admin", Email = "admin3@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var service = CreateService(db, llm);
        await service.AnalyzeAsync(new[] { review.Id });

        var ok = await service.AcknowledgeAlertAsync(review.Id, admin.Id);

        Assert.True(ok);
        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentAlertStatus.Acknowledged, sentiment.AlertStatus);
        Assert.Equal(admin.Id, sentiment.AcknowledgedById);
    }

    // ============ Backfill ============

    [Fact]
    public async Task EnqueueBackfillAsync_OnlyEnqueuesUnanalyzedReviews_InChunks()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var analyzed = AddReview(db, product.Id, user.Id, 5, "Có comment");
        var unanalyzed = AddReview(db, product.Id, user.Id, 5, "Cần phân tích");

        // Review đầu tiên đã có sentiment
        db.ReviewSentiments.Add(new ReviewSentiment { ReviewId = analyzed.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback });
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient();
        var service = CreateService(db, llm);

        var chunks = await service.EnqueueBackfillAsync();

        Assert.Equal(1, chunks);
        var messages = await db.OutboxMessages.Where(m => m.Type == OutboxMessageTypes.ReviewSentimentBackfill).ToListAsync();
        Assert.Single(messages);
        Assert.Contains("sentiment-backfill-", messages[0].IdempotencyKey);
    }

    [Fact]
    public async Task EnqueueBackfillAsync_Twice_EnqueuesFreshBatch_DespiteOldProcessedMessage()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 5, "Cần phân tích");

        // Message backfill cũ đã xử lý giữ idempotency key — không được chặn đợt mới
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = OutboxMessageTypes.ReviewSentimentBackfill,
            Payload = """{"reviewIds":[1]}""",
            IdempotencyKey = "sentiment-backfill-7-7",
            OccurredAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow,
            ProcessedAtUtc = DateTime.UtcNow,
            AttemptCount = 1
        });
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient();
        var service = CreateService(db, llm);

        var chunks = await service.EnqueueBackfillAsync();
        Assert.Equal(1, chunks);

        var pending = await db.OutboxMessages
            .Where(m => m.Type == OutboxMessageTypes.ReviewSentimentBackfill && m.ProcessedAtUtc == null)
            .ToListAsync();
        Assert.Single(pending);
        Assert.DoesNotContain(pending, m => m.IdempotencyKey == "sentiment-backfill-7-7");
    }

    [Fact]
    public async Task CountUnanalyzedAsync_CountsMissingAndFailed()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var r1 = AddReview(db, product.Id, user.Id, 5, "a");
        var r2 = AddReview(db, product.Id, user.Id, 5, "b");
        var r3 = AddReview(db, product.Id, user.Id, 5, "c");
        var r4 = AddReview(db, product.Id, user.Id, 5, "d");

        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = r1.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback },
            new ReviewSentiment { ReviewId = r2.Id, Sentiment = SentimentLabel.Failed, Source = SentimentSource.AiModel },
            new ReviewSentiment { ReviewId = r3.Id, Sentiment = SentimentLabel.Negative, Source = SentimentSource.RatingFallback });
        await db.SaveChangesAsync();

        var service = CreateService(db, new FakeLlmClient());
        Assert.Equal(2, await service.CountUnanalyzedAsync()); // r2 (Failed) + r4 (chưa có)
    }

    // ============ Dashboard ============

    [Fact]
    public async Task GetDashboardAsync_ComputesDistributionAndTopProducts()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var (user2, _, product2) = (new User { Name = "B", Email = "b@test.com", Password = "x" }, new Category(), new Product());
        // tạo thêm sản phẩm 2 + category 2
        var cat2 = new Category { Name = "Rau", Slug = "rau" };
        db.Categories.Add(cat2);
        await db.SaveChangesAsync();
        product2 = new Product { Name = "Cà rốt", Slug = "ca-rot", CategoryId = cat2.Id, Price = 30000m, DisplayMinPrice = 30000m, DisplayMaxPrice = 30000m };
        db.Products.Add(product2);
        db.Users.Add(user2);
        await db.SaveChangesAsync();

        var pos = AddReview(db, product.Id, user.Id, 5, "ngon");
        var neg = AddReview(db, product.Id, user2.Id, 1, "dở");
        var neu = AddReview(db, product2.Id, user.Id, 3, "tạm");

        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = pos.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback },
            new ReviewSentiment { ReviewId = neg.Id, Sentiment = SentimentLabel.Negative, Severity = 3, Source = SentimentSource.RatingFallback, AlertStatus = SentimentAlertStatus.Pending },
            new ReviewSentiment { ReviewId = neu.Id, Sentiment = SentimentLabel.Neutral, Source = SentimentSource.RatingFallback });
        await db.SaveChangesAsync();

        var service = CreateService(db, new FakeLlmClient());
        var data = await service.GetDashboardAsync();

        Assert.Equal(3, data.TotalAnalyzed);
        Assert.Equal(1, data.PositiveCount);
        Assert.Equal(1, data.NeutralCount);
        Assert.Equal(1, data.NegativeCount);
        Assert.Equal(1, data.PendingAlertCount);
        Assert.Equal(14, data.Trend.Count);
        Assert.Equal(2, data.TopNegativeProducts.Count);
        Assert.Equal("Táo Fuji", data.TopNegativeProducts[0].ProductName);
        Assert.Equal(1, data.TopNegativeProducts[0].NegativeCount);
    }

    [Fact]
    public async Task GetDashboardAsync_ExcludesPendingConflictFromOperationalKpis()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var approved = AddReview(db, product.Id, user.Id, 5, "Ngon");
        var pending = AddReview(db, product.Id, user.Id, 5, "Trái cây bị hư");

        db.ReviewSentiments.AddRange(
            new ReviewSentiment
            {
                ReviewId = approved.Id,
                Sentiment = SentimentLabel.Positive,
                RatingSentiment = SentimentLabel.Positive,
                CommentSentiment = SentimentLabel.Positive,
                Source = SentimentSource.AiModel
            },
            new ReviewSentiment
            {
                ReviewId = pending.Id,
                Sentiment = SentimentLabel.Negative,
                RatingSentiment = SentimentLabel.Positive,
                CommentSentiment = SentimentLabel.Negative,
                HasRatingCommentConflict = true,
                NeedsManualReview = true,
                HasSafetyRisk = true,
                Severity = 3,
                Source = SentimentSource.AiModel
            });
        await db.SaveChangesAsync();

        var data = await CreateService(db, new FakeLlmClient()).GetDashboardAsync();

        Assert.Equal(1, data.TotalAnalyzed);
        Assert.Equal(0, data.NegativeCount);
        Assert.Equal(1, data.PendingReviewCount);
        Assert.Equal(1, data.ConflictCount);
        Assert.Equal(1, data.SafetyRiskCount);
    }

    // ============ Outbox hook khi tạo review ============

    [Fact]
    public async Task CreateReviewAsync_EnqueuesSentimentMessage()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);

        var service = new ReviewService(
            new Fruitables.Repositories.UnitOfWork(db),
            Mock.Of<Fruitables.Services.IWordMaskingService>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReviewService>.Instance,
            new OutboxService(db, TimeProvider.System),
            Options().AsOptions());

        var result = await service.CreateReviewAsync(new CreateReviewDto { ProductId = product.Id, Rating = 5, Comment = "Ngon" }, user.Id);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var message = await db.OutboxMessages.SingleAsync(m => m.Type == OutboxMessageTypes.ReviewSentimentAnalyze);
        Assert.Equal($"sentiment-{result.Data!.Id}", message.IdempotencyKey);
        Assert.Contains($"\"reviewId\":{result.Data.Id}", message.Payload);
    }

    [Fact]
    public async Task CreateReviewAsync_EnqueueFailure_RollsBackReviewAtomically()
    {
        var options = TestDbContextFactory.CreateSqliteOptions();
        int userId, productId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var user = new User { Name = "A", Email = "rollback@test.com", Password = "x" };
            var cat = new Category { Name = "C", Slug = "c-rb" };
            seed.Users.Add(user);
            seed.Categories.Add(cat);
            await seed.SaveChangesAsync();
            var product = new Product { Name = "P", Slug = "p-rb", CategoryId = cat.Id, Price = 1000m, DisplayMinPrice = 1000m, DisplayMaxPrice = 1000m };
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            userId = user.Id;
            productId = product.Id;
        }

        var failingOutbox = new Mock<IOutboxService>();
        failingOutbox
            .Setup(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox down"));

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new ReviewService(
                new UnitOfWork(db),
                Mock.Of<Fruitables.Services.IWordMaskingService>(),
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<ReviewService>.Instance,
                failingOutbox.Object,
                Options().AsOptions());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateReviewAsync(new CreateReviewDto { ProductId = productId, Rating = 5, Comment = "Ngon" }, userId));
        }

        // Transaction rollback → không để lại review "mồ côi" hay outbox message dở dang.
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.Reviews.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }

    // ============ Outbox handler ============

    [Fact]
    public async Task SentimentHandler_DispatchesAnalyzeMessage()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 5, "Rất tuyệt");

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"positive","confidence":0.95,"reason":"khen","aspects":[]}]}"""
        };
        var notifier = new Mock<IRealtimeNotifier>();
        _ = CreateService(db, llm, notifier); // đăng ký service trong scope test? không cần — handler tự tạo scope
        var handler = new SentimentAnalysisOutboxHandler(new FakeScopeFactory(db, llm, notifier.Object), NullLogger<SentimentAnalysisOutboxHandler>.Instance);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = OutboxMessageTypes.ReviewSentimentAnalyze,
            Payload = $$"""{"reviewId":{{review.Id}}}""",
            IdempotencyKey = "test-key"
        };

        Assert.True(handler.CanHandle(message.Type));
        await handler.HandleAsync(message, CancellationToken.None);

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentLabel.Positive, sentiment.Sentiment);
    }

    // ============ Testimonial suggest từ review tích cực ============

    [Fact]
    public async Task SuggestFromReviewAsync_PositiveReview_CreatesPendingTestimonial()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 5, "Trái cây rất tươi, đóng gói cẩn thận, sẽ mua tiếp");

        db.ReviewSentiments.Add(new ReviewSentiment
        {
            ReviewId = review.Id,
            Sentiment = SentimentLabel.Positive,
            RatingSentiment = SentimentLabel.Positive,
            CommentSentiment = SentimentLabel.Positive,
            Source = SentimentSource.AiModel
        });
        await db.SaveChangesAsync();

        var storedSentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentLabel.Positive, storedSentiment.Sentiment);
        Assert.Equal(SentimentLabel.Positive, storedSentiment.RatingSentiment);
        Assert.Equal(SentimentLabel.Positive, storedSentiment.CommentSentiment);

        var service = new TestimonialService(new UnitOfWork(db));
        var testimonial = await service.SuggestFromReviewAsync(review.Id);

        Assert.NotNull(testimonial);
        Assert.False(testimonial!.IsActive); // chờ admin duyệt
        Assert.Equal(user.Name, testimonial.Name);
        Assert.Equal(review.Comment, testimonial.Content);
        Assert.Equal(5, testimonial.Rating);
    }

    [Fact]
    public async Task SuggestFromReviewAsync_NegativeOrRatingLow_ReturnsNull()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var neg = AddReview(db, product.Id, user.Id, 1, "Hàng hỏng");
        var low = AddReview(db, product.Id, user.Id, 3, "Tạm ổn");
        var conflict = AddReview(db, product.Id, user.Id, 5, "Trái cây bị hư");
        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = neg.Id, Sentiment = SentimentLabel.Negative, Severity = 2, Source = SentimentSource.AiModel },
            new ReviewSentiment { ReviewId = low.Id, Sentiment = SentimentLabel.Neutral, Source = SentimentSource.AiModel },
            new ReviewSentiment
            {
                ReviewId = conflict.Id,
                Sentiment = SentimentLabel.Negative,
                RatingSentiment = SentimentLabel.Positive,
                CommentSentiment = SentimentLabel.Negative,
                HasRatingCommentConflict = true,
                NeedsManualReview = true,
                Source = SentimentSource.AiModel
            });
        await db.SaveChangesAsync();

        var service = new TestimonialService(new UnitOfWork(db));

        Assert.Null(await service.SuggestFromReviewAsync(neg.Id));
        Assert.Null(await service.SuggestFromReviewAsync(low.Id));
        Assert.Null(await service.SuggestFromReviewAsync(conflict.Id));
    }

    // ============ Chatbot RAG: tóm tắt cảm xúc sản phẩm ============

    [Fact]
    public async Task IndexProductReviewSummaryAsync_CreatesChunk_WithCountsAndKeywords()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var pos = AddReview(db, product.Id, user.Id, 5, "Rất ngon");
        var neg = AddReview(db, product.Id, user.Id, 1, "Táo dập, giao trễ");
        var pending = AddReview(db, product.Id, user.Id, 5, "Trái cây bị hư");

        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = pos.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.AiModel },
            new ReviewSentiment { ReviewId = neg.Id, Sentiment = SentimentLabel.Negative, Severity = 2, Source = SentimentSource.AiModel },
            new ReviewSentiment
            {
                ReviewId = pending.Id,
                Sentiment = SentimentLabel.Negative,
                CommentSentiment = SentimentLabel.Negative,
                RatingSentiment = SentimentLabel.Positive,
                HasRatingCommentConflict = true,
                NeedsManualReview = true,
                Severity = 3,
                Source = SentimentSource.AiModel
            });
        await db.SaveChangesAsync();

        var service = new IndexingService(db, new DeterministicEmbeddingClient(), Microsoft.Extensions.Options.Options.Create(new ChatOptions()), NullLogger<IndexingService>.Instance);
        await service.IndexProductReviewSummaryAsync(product.Id);

        var chunk = await db.KnowledgeChunks.SingleOrDefaultAsync(c => c.SourceType == KnowledgeSourceType.ReviewSummary && c.SourceId == product.Id.ToString());
        Assert.NotNull(chunk);
        Assert.Contains("50%", chunk!.Content);   // 1/2 tích cực → 50% hài lòng
        Assert.Contains("tiêu cực 1", chunk.Content);
        Assert.Contains("Từ khóa: đánh giá", chunk.Content);
    }

    [Fact]
    public async Task IndexProductReviewSummaryAsync_NoSentiments_DeactivatesSource()
    {
        await using var db = CreateContext();
        var (_, _, product) = SeedCatalog(db);
        db.KnowledgeChunks.Add(new KnowledgeChunk
        {
            SourceType = KnowledgeSourceType.ReviewSummary,
            SourceId = product.Id.ToString(),
            Title = "cũ",
            Content = "cũ",
            EmbeddingJson = "[]",
            ContentHash = "x",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new IndexingService(db, new DeterministicEmbeddingClient(), Microsoft.Extensions.Options.Options.Create(new ChatOptions()), NullLogger<IndexingService>.Instance);
        await service.IndexProductReviewSummaryAsync(product.Id);

        var chunk = await db.KnowledgeChunks.SingleAsync(c => c.SourceType == KnowledgeSourceType.ReviewSummary && c.SourceId == product.Id.ToString());
        Assert.False(chunk.IsActive);
    }

    // ============ Dashboard widget sentiment ============

    [Fact]
    public async Task DashboardService_GetSentimentStatisticsAsync_Counts7dAndAlerts()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var pos = AddReview(db, product.Id, user.Id, 5, "ok", DateTime.UtcNow.AddDays(-1));
        var neg = AddReview(db, product.Id, user.Id, 1, "tệ", DateTime.UtcNow);
        var old = AddReview(db, product.Id, user.Id, 5, "ok cũ", DateTime.UtcNow.AddDays(-10));

        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = pos.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback },
            new ReviewSentiment { ReviewId = neg.Id, Sentiment = SentimentLabel.Negative, Severity = 3, Source = SentimentSource.RatingFallback, AlertStatus = SentimentAlertStatus.Pending },
            new ReviewSentiment { ReviewId = old.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback });
        await db.SaveChangesAsync();

        var dashboard = new DashboardService(new UnitOfWork(db));
        var stats = await dashboard.GetSentimentStatisticsAsync();

        Assert.Equal(1, stats.Negative7d);    // chỉ review 7 ngày
        Assert.Equal(2, stats.Total7d);
        Assert.Equal(1, stats.PendingAlerts);
    }

    // ============ P1-1: Safety word-boundary (chặn false positive) ============

    [Fact]
    public void HasSafetyRisk_DoesNotMatchCommonWordsContainingSignal()
    {
        // "ôi" từng khớp bên trong "tôi" (IndexOf cũ) → false positive diện rộng.
        Assert.False(SentimentDecisionResolver.HasSafetyRisk("Tôi rất hài lòng"));
        Assert.False(SentimentDecisionResolver.HasSafetyRisk("Tôi thấy sản phẩm bình thường"));
    }

    [Fact]
    public void HasSafetyRisk_MatchesWholeWordSignals()
    {
        Assert.True(SentimentDecisionResolver.HasSafetyRisk("Táo bị ôi rồi"));
        Assert.True(SentimentDecisionResolver.HasSafetyRisk("Trái cây bị mốc"));
        Assert.True(SentimentDecisionResolver.HasSafetyRisk("Cam có dòi"));
    }

    // ============ P1-6: Confidence tham gia quyết định ============

    [Fact]
    public void Resolver_LowOrNullConfidence_ForcesManualReview()
    {
        var low = SentimentDecisionResolver.Resolve(5, "ngon", SentimentLabel.Positive, null, 0.2, "x", Options());
        Assert.True(low.NeedsManualReview);

        var high = SentimentDecisionResolver.Resolve(5, "ngon", SentimentLabel.Positive, null, 0.9, "x", Options());
        Assert.False(high.NeedsManualReview);

        var nullConf = SentimentDecisionResolver.Resolve(5, "ngon", SentimentLabel.Positive, null, null, "x", Options());
        Assert.True(nullConf.NeedsManualReview);
    }

    // ============ P1-3: Vòng đời cảnh báo ============

    [Fact]
    public async Task AnalyzeAsync_ReanalysisSeverityDowngrade_ClearsAlertAndAckMetadata()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 1, "Thái độ phục vụ kém, rất thất vọng"); // không chứa tín hiệu an toàn
        var admin = new User { Name = "Admin", Email = "down-admin@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var severe = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":3,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var service = CreateService(db, severe);
        await service.AnalyzeAsync(new[] { review.Id });
        await service.AcknowledgeAlertAsync(review.Id, admin.Id);
        Assert.Equal(SentimentAlertStatus.Acknowledged, (await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id)).AlertStatus);

        // Phân tích lại: vẫn Negative nhưng severity giảm 3 → 2 (< ngưỡng) → gỡ cảnh báo + xóa ack.
        var milder = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":2,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        await CreateService(db, milder).AnalyzeAsync(new[] { review.Id });

        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentAlertStatus.None, sentiment.AlertStatus);
        Assert.Null(sentiment.AcknowledgedById);
        Assert.Null(sentiment.AcknowledgedAtUtc);
    }

    [Fact]
    public async Task OverrideAsync_ToSevereNegative_CreatesAlertAndNotifies()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 2, "Giao hơi chậm"); // negative nhẹ, không safety
        var admin = new User { Name = "Admin", Email = "ovr-admin@test.com", Password = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":1,"confidence":0.9,"reason":"x","aspects":[]}]}"""
        };
        var notifier = new Mock<IRealtimeNotifier>();
        var service = CreateService(db, llm, notifier);
        await service.AnalyzeAsync(new[] { review.Id });
        Assert.Equal(SentimentAlertStatus.None, (await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id)).AlertStatus);

        var ok = await service.OverrideAsync(review.Id, SentimentLabel.Negative, 3, "Đã xác minh mức nghiêm trọng", admin.Id);

        Assert.True(ok);
        var sentiment = await db.ReviewSentiments.SingleAsync(s => s.ReviewId == review.Id);
        Assert.Equal(SentimentAlertStatus.Pending, sentiment.AlertStatus);
        notifier.Verify(n => n.NotifySevereReviewAlertAsync(review.Id, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ============ P2-1: Aspect gắn đúng review (join theo SentimentId) ============

    [Fact]
    public async Task GetReviewsAsync_AttachesAspectsBySentimentId_NotReviewId()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var r1 = AddReview(db, product.Id, user.Id, 5, "Ngon");        // Review.Id = 1
        _ = AddReview(db, product.Id, user.Id, 3, "Tạm");              // Review.Id = 2 (không có sentiment)
        var r3 = AddReview(db, product.Id, user.Id, 1, "Dở");          // Review.Id = 3

        // Sentiment cho r3 trước (Id=1) rồi r1 (Id=2) → ReviewSentiment.Id lệch Review.Id.
        var s3 = new ReviewSentiment { ReviewId = r3.Id, Sentiment = SentimentLabel.Negative, Severity = 2, Source = SentimentSource.AiModel };
        db.ReviewSentiments.Add(s3);
        await db.SaveChangesAsync();
        var s1 = new ReviewSentiment { ReviewId = r1.Id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.AiModel };
        db.ReviewSentiments.Add(s1);
        await db.SaveChangesAsync();
        Assert.Equal(1, s3.Id);
        Assert.Equal(2, s1.Id);

        db.ReviewSentimentAspects.AddRange(
            new ReviewSentimentAspect { ReviewSentimentId = s3.Id, Aspect = SentimentAspect.Quality, Sentiment = SentimentLabel.Negative, Severity = 2 },
            new ReviewSentimentAspect { ReviewSentimentId = s1.Id, Aspect = SentimentAspect.Delivery, Sentiment = SentimentLabel.Positive });
        await db.SaveChangesAsync();

        var result = await CreateService(db, new FakeLlmClient()).GetReviewsAsync(new SentimentReviewFilter { PageSize = 10 });

        var rowR1 = result.Items.Single(i => i.ReviewId == r1.Id);
        var rowR3 = result.Items.Single(i => i.ReviewId == r3.Id);
        Assert.Single(rowR1.Aspects);
        Assert.Equal(nameof(SentimentAspect.Delivery), rowR1.Aspects[0].Aspect);
        Assert.Single(rowR3.Aspects);
        Assert.Equal(nameof(SentimentAspect.Quality), rowR3.Aspects[0].Aspect);
    }

    // ============ P2-2: Aspect "other" không bị bỏ ============

    [Theory]
    [InlineData("other", true)]
    [InlineData("khac", true)]
    [InlineData("banana", false)]
    public void TryMapAspect_MapsOther_AndRejectsUnknown(string input, bool expected)
    {
        var ok = SentimentPromptBuilder.TryMapAspect(input, out var result);
        Assert.Equal(expected, ok);
        if (ok) Assert.Equal(SentimentAspect.Other, result);
    }

    [Fact]
    public async Task AnalyzeAsync_AspectOther_IsPersisted()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var review = AddReview(db, product.Id, user.Id, 2, "Khó chịu chung chung");

        var llm = new FakeLlmClient
        {
            JsonResponse = """{"results":[{"reviewId":1,"sentiment":"negative","severity":2,"confidence":0.9,"reason":"x","aspects":[{"aspect":"other","sentiment":"negative","severity":2}]}]}"""
        };
        await CreateService(db, llm).AnalyzeAsync(new[] { review.Id });

        var sentiment = await db.ReviewSentiments.Include(s => s.Aspects).SingleAsync(s => s.ReviewId == review.Id);
        Assert.Single(sentiment.Aspects);
        Assert.Equal(SentimentAspect.Other, sentiment.Aspects.First().Aspect);
    }

    // ============ P2-3: KPI nhất quán (loại review ẩn ở mọi nơi) ============

    [Fact]
    public async Task GetDashboardAsync_ExcludesHiddenReviews_ConsistentlyAcrossKpis()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        var visible = AddReview(db, product.Id, user.Id, 1, "Tệ");
        var hidden = AddReview(db, product.Id, user.Id, 1, "Rất tệ");
        hidden.IsHidden = true;
        await db.SaveChangesAsync();

        db.ReviewSentiments.AddRange(
            new ReviewSentiment { ReviewId = visible.Id, Sentiment = SentimentLabel.Negative, Severity = 2, Source = SentimentSource.AiModel },
            new ReviewSentiment { ReviewId = hidden.Id, Sentiment = SentimentLabel.Negative, Severity = 2, Source = SentimentSource.AiModel });
        await db.SaveChangesAsync();
        db.ReviewSentimentAspects.AddRange(
            new ReviewSentimentAspect { ReviewSentimentId = visible.Id, Aspect = SentimentAspect.Quality, Sentiment = SentimentLabel.Negative },
            new ReviewSentimentAspect { ReviewSentimentId = hidden.Id, Aspect = SentimentAspect.Quality, Sentiment = SentimentLabel.Negative });
        await db.SaveChangesAsync();

        var data = await CreateService(db, new FakeLlmClient()).GetDashboardAsync();

        Assert.Equal(1, data.TotalAnalyzed); // chỉ review visible
        Assert.Equal(1, data.NegativeCount);
        Assert.Single(data.TopNegativeProducts);
        Assert.Equal(1, data.TopNegativeProducts[0].NegativeCount);
        Assert.Single(data.TopNegativeAspects); // aspect của review ẩn bị loại
        Assert.Equal(1, data.TopNegativeAspects[0].Count);
    }

    // ============ P2-4: CSV escape + maxPageSize export ============

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("normal", "normal")]
    public void CsvEscaper_NeutralizesFormulaInjection(string input, string expected)
    {
        Assert.Equal(expected, CsvEscaper.Escape(input));
    }

    [Fact]
    public void CsvEscaper_QuotesAndEscapesDelimiters()
    {
        Assert.Equal("\"a,b\"", CsvEscaper.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", CsvEscaper.Escape("say \"hi\""));
    }

    [Fact]
    public async Task GetReviewsAsync_MaxPageSize_ClampsExportSize()
    {
        await using var db = CreateContext();
        var (user, _, product) = SeedCatalog(db);
        for (var i = 0; i < 5; i++) AddReview(db, product.Id, user.Id, 5, $"Nhận xét {i}");
        for (var id = 1; id <= 5; id++)
            db.ReviewSentiments.Add(new ReviewSentiment { ReviewId = id, Sentiment = SentimentLabel.Positive, Source = SentimentSource.RatingFallback });
        await db.SaveChangesAsync();

        var service = CreateService(db, new FakeLlmClient());
        var normal = await service.GetReviewsAsync(new SentimentReviewFilter { PageSize = 5 });
        Assert.Equal(5, normal.Items.Count);

        var clamped = await service.GetReviewsAsync(new SentimentReviewFilter { PageSize = 5 }, maxPageSize: 2);
        Assert.Equal(2, clamped.Items.Count);
    }

    // LLM trả lỗi N lần đầu rồi thành công
    private sealed class FlakyLlmClient : ILlmClient
    {
        private readonly int _failures;
        private readonly string _success;
        public List<(string System, string User)> Calls { get; } = new();

        public FlakyLlmClient(int failures, string success)
        {
            _failures = failures;
            _success = success;
        }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<string> CompleteStreamingAsync(string systemPrompt, string userPrompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield break;
        }

        public Task<System.Text.Json.JsonElement> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            Calls.Add((systemPrompt, userPrompt));
            if (Calls.Count <= _failures)
                throw new InvalidOperationException("LLM provider error (empty response)");
            return Task.FromResult(System.Text.Json.JsonDocument.Parse(_success).RootElement.Clone());
        }
    }

    // Scope factory đơn giản cho test handler
    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly ApplicationDbContext _db;
        private readonly ILlmClient _llm;
        private readonly IRealtimeNotifier _notifier;

        public FakeScopeFactory(ApplicationDbContext db, ILlmClient llm, IRealtimeNotifier notifier)
        {
            _db = db;
            _llm = llm;
            _notifier = notifier;
        }

        public IServiceScope CreateScope() => new FakeScope(_db, _llm, _notifier);

        private sealed class FakeScope : IServiceScope
        {
            private readonly ApplicationDbContext _db;
            private readonly ILlmClient _llm;
            private readonly IRealtimeNotifier _notifier;
            private readonly IServiceProvider _provider;

            public FakeScope(ApplicationDbContext db, ILlmClient llm, IRealtimeNotifier notifier)
            {
                _db = db;
                _llm = llm;
                _notifier = notifier;
                var services = new ServiceCollection();
                services.AddSingleton<ISentimentAnalysisService>(new SentimentAnalysisService(
                    _db, _llm, new OutboxService(_db, TimeProvider.System),
                    Microsoft.Extensions.Options.Options.Create(new SentimentOptions()), _notifier, NullLogger<SentimentAnalysisService>.Instance));
                _provider = services.BuildServiceProvider();
            }

            public IServiceProvider ServiceProvider => _provider;
            public void Dispose() { }
        }
    }
}
