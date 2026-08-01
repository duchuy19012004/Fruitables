namespace Fruitables.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 2;
    // Thời gian giữ lock (giây) cho mỗi message. Phải phủ được worst-case xử lý 1 message
    // (LLM timeout 120s × ~3 lần retry ≈ 360s). Worker gia hạn lock trước từng message.
    public int LockSeconds { get; set; } = 900;
    public int MaxAttempts { get; set; } = 8;
    public int BaseRetrySeconds { get; set; } = 5;
    public int MaxRetrySeconds { get; set; } = 900;
    public int ProcessedRetentionDays { get; set; } = 30;
    public int DeadLetterRetentionDays { get; set; } = 90;
}
