using Fruitables.Models;

namespace Fruitables.Services.Outbox;

public interface IOutboxMessageHandler
{
    bool CanHandle(string messageType);
    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public sealed class ReturnDomainEventOutboxHandler(ILogger<ReturnDomainEventOutboxHandler> logger) : IOutboxMessageHandler
{
    public bool CanHandle(string messageType) => messageType.StartsWith("returns.", StringComparison.Ordinal);

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Phase 2.2 adds email/SignalR consumers that use message.Id as their idempotency key.
        logger.LogInformation("Processed return outbox message {MessageId} ({MessageType})", message.Id, message.Type);
        return Task.CompletedTask;
    }
}
