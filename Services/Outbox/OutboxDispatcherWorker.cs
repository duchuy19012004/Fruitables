using Fruitables.Options;
using Fruitables.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Fruitables.Services.Outbox;

public sealed class OutboxDispatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcherWorker> _logger;
    private readonly string _instanceId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public OutboxDispatcherWorker(IServiceScopeFactory scopeFactory, TimeProvider clock, IOptions<OutboxOptions> options, ILogger<OutboxDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextCleanupAtUtc = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
                var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>().ToArray();
                var messages = await outbox.ClaimAsync(
                    Math.Clamp(_options.BatchSize, 1, 200),
                    _instanceId,
                    TimeSpan.FromSeconds(Math.Max(10, _options.LockSeconds)),
                    stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        var matchingHandlers = handlers.Where(x => x.CanHandle(message.Type)).ToArray();
                        if (matchingHandlers.Length == 0) throw new InvalidOperationException($"No outbox handler is registered for '{message.Type}'.");
                        foreach (var handler in matchingHandlers) await handler.HandleAsync(message, stoppingToken);
                        await outbox.CompleteAsync(message.Id, _instanceId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        await outbox.FailAsync(
                            message.Id,
                            _instanceId,
                            ex,
                            Math.Max(1, _options.MaxAttempts),
                            TimeSpan.FromSeconds(Math.Max(1, _options.BaseRetrySeconds)),
                            TimeSpan.FromSeconds(Math.Max(Math.Max(1, _options.BaseRetrySeconds), _options.MaxRetrySeconds)),
                            stoppingToken);
                        _logger.LogWarning(ex, "Outbox message {MessageId} ({MessageType}) failed on attempt {Attempt}", message.Id, message.Type, message.AttemptCount);
                    }
                }

                var now = _clock.GetUtcNow().UtcDateTime;
                if (now >= nextCleanupAtUtc)
                {
                    await outbox.CleanupAsync(
                        TimeSpan.FromDays(Math.Max(1, _options.ProcessedRetentionDays)),
                        TimeSpan.FromDays(Math.Max(1, _options.DeadLetterRetentionDays)),
                        stoppingToken);
                    nextCleanupAtUtc = now.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Outbox dispatch cycle failed."); }

            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), _clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
