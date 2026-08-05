namespace Fruitables.Services.Outbox;

public static class OutboxMessageTypes
{
    public const string ReviewSentimentAnalyze = "reviews.sentiment.analyze";
    public const string ReviewSentimentBackfill = "reviews.sentiment.backfill";
}
