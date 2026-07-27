using Fruitables.Models;

namespace Fruitables.Services.Interfaces;

public interface IOutboxService
{
    Task<OutboxMessage> EnqueueAsync(string type, object payload, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, string lockToken, TimeSpan lockDuration, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid messageId, string lockToken, CancellationToken cancellationToken = default);
    Task<bool> FailAsync(Guid messageId, string lockToken, Exception exception, int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay, CancellationToken cancellationToken = default);
    Task<int> CleanupAsync(TimeSpan processedRetention, TimeSpan deadLetterRetention, CancellationToken cancellationToken = default);
}
