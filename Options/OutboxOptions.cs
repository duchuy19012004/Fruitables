namespace Fruitables.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 2;
    public int LockSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 8;
    public int BaseRetrySeconds { get; set; } = 5;
    public int MaxRetrySeconds { get; set; } = 900;
    public int ProcessedRetentionDays { get; set; } = 30;
    public int DeadLetterRetentionDays { get; set; } = 90;
}
