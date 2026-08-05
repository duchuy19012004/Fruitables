using System.Text.Json;
using Fruitables.Models;
using Fruitables.Services.Communications;
using Microsoft.Extensions.Logging;
using Fruitables.Services.Sentiment;

namespace Fruitables.Services.Outbox;

// Xử lý message phân tích cảm xúc review:
//  - reviews.sentiment.analyze   → phân tích 1 review (realtime, khi tạo/sửa review)
//  - reviews.sentiment.backfill  → phân tích 1 chunk review (backfill review cũ)
public sealed class SentimentAnalysisOutboxHandler : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SentimentAnalysisOutboxHandler> _logger;

    public SentimentAnalysisOutboxHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<SentimentAnalysisOutboxHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool CanHandle(string messageType)
        => messageType.StartsWith("reviews.sentiment.", StringComparison.Ordinal);

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISentimentAnalysisService>();

        if (message.Type == OutboxMessageTypes.ReviewSentimentAnalyze)
        {
            var payload = JsonSerializer.Deserialize<AnalyzePayload>(message.Payload, JsonOptions);
            if (payload is null || payload.ReviewId <= 0)
                throw new InvalidOperationException($"Invalid analyze payload for message {message.Id}");

            var count = await service.AnalyzeAsync(new[] { payload.ReviewId }, cancellationToken);
            _logger.LogInformation("Sentiment analyzed review {ReviewId} (message {MessageId})", payload.ReviewId, message.Id);
            if (count == 0) return;
        }
        else if (message.Type == OutboxMessageTypes.ReviewSentimentBackfill)
        {
            var payload = JsonSerializer.Deserialize<BackfillPayload>(message.Payload, JsonOptions);
            if (payload is null || payload.ReviewIds is null || payload.ReviewIds.Length == 0)
                throw new InvalidOperationException($"Invalid backfill payload for message {message.Id}");

            var count = await service.AnalyzeAsync(payload.ReviewIds, cancellationToken);
            _logger.LogInformation("Sentiment backfill analyzed {Count} reviews (message {MessageId})", count, message.Id);
        }
        else
        {
            throw new InvalidOperationException($"Unknown sentiment message type '{message.Type}'");
        }
    }

    private sealed class AnalyzePayload
    {
        public int ReviewId { get; set; }
    }

    private sealed class BackfillPayload
    {
        public int[] ReviewIds { get; set; } = [];
    }
}
