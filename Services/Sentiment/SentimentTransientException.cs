namespace Fruitables.Services.Sentiment;

/// <summary>
/// Lỗi hạ tầng tạm thời khi gọi LLM (timeout, HTTP 429/500, JSON hỏng) sau khi đã retry trong call.
/// Ném ra để outbox dispatcher retry với backoff và dead-letter khi hết attempt,
/// thay vì đánh dấu review là Failed vĩnh viễn chỉ vì một đợt lỗi LLM tạm thời.
/// </summary>
public sealed class SentimentTransientException : Exception
{
    public SentimentTransientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
