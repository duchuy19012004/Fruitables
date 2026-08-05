using Fruitables.Models;

namespace Fruitables.Services.Outbox;

public interface IOutboxMessageHandler
{
    bool CanHandle(string messageType);
    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}
