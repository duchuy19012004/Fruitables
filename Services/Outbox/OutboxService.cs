using System.Data;
using System.Text.Json;
using Fruitables.Data;
using Fruitables.Models;
using Fruitables.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fruitables.Services.Outbox;

public sealed class OutboxService : IOutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public OutboxService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OutboxMessage> EnqueueAsync(string type, object payload, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (type.Length > 200 || idempotencyKey.Length > 200) throw new ArgumentOutOfRangeException(nameof(idempotencyKey), "Outbox type and idempotency key must not exceed 200 characters.");

        var local = _db.OutboxMessages.Local.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (local != null) return local;
        var existing = await _db.OutboxMessages.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing != null) return existing;

        var now = _clock.GetUtcNow().UtcDateTime;
        var message = new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            IdempotencyKey = idempotencyKey,
            OccurredAtUtc = now,
            NextAttemptAtUtc = now
        };
        _db.OutboxMessages.Add(message);
        return message;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, string lockToken, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) return [];
        ArgumentException.ThrowIfNullOrWhiteSpace(lockToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        var lockedUntil = now.Add(lockDuration);
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        List<OutboxMessage> messages;
        if (_db.Database.IsSqlServer())
        {
            messages = await _db.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT TOP ({batchSize}) *
                    FROM [OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE [ProcessedAtUtc] IS NULL
                      AND [DeadLetteredAtUtc] IS NULL
                      AND [NextAttemptAtUtc] <= {now}
                      AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= {now})
                    ORDER BY [NextAttemptAtUtc], [OccurredAtUtc]
                    """)
                .ToListAsync(cancellationToken);
        }
        else
        {
            messages = await _db.OutboxMessages
                .Where(x => x.ProcessedAtUtc == null && x.DeadLetteredAtUtc == null && x.NextAttemptAtUtc <= now && (x.LockedUntilUtc == null || x.LockedUntilUtc <= now))
                .OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.OccurredAtUtc)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        foreach (var message in messages)
        {
            message.LockToken = lockToken;
            message.LockedUntilUtc = lockedUntil;
            message.AttemptCount++;
        }
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    public async Task<bool> CompleteAsync(Guid messageId, string lockToken, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == messageId && x.LockToken == lockToken && x.ProcessedAtUtc == null, cancellationToken);
        if (message == null) return false;
        message.ProcessedAtUtc = _clock.GetUtcNow().UtcDateTime;
        message.LockToken = null;
        message.LockedUntilUtc = null;
        message.LastError = null;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> FailAsync(Guid messageId, string lockToken, Exception exception, int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == messageId && x.LockToken == lockToken && x.ProcessedAtUtc == null, cancellationToken);
        if (message == null) return false;
        var now = _clock.GetUtcNow().UtcDateTime;
        message.LastError = exception.ToString()[..Math.Min(exception.ToString().Length, 4000)];
        message.LockToken = null;
        message.LockedUntilUtc = null;
        if (message.AttemptCount >= maxAttempts)
        {
            message.DeadLetteredAtUtc = now;
        }
        else
        {
            var exponent = Math.Min(30, Math.Max(0, message.AttemptCount - 1));
            var delaySeconds = Math.Min(maxDelay.TotalSeconds, baseDelay.TotalSeconds * Math.Pow(2, exponent));
            message.NextAttemptAtUtc = now.AddSeconds(delaySeconds);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> CleanupAsync(TimeSpan processedRetention, TimeSpan deadLetterRetention, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var processedBefore = now.Subtract(processedRetention);
        var deadLetterBefore = now.Subtract(deadLetterRetention);
        var query = _db.OutboxMessages.Where(x =>
            (x.ProcessedAtUtc != null && x.ProcessedAtUtc < processedBefore) ||
            (x.DeadLetteredAtUtc != null && x.DeadLetteredAtUtc < deadLetterBefore));
        if (_db.Database.IsRelational()) return await query.ExecuteDeleteAsync(cancellationToken);
        var messages = await query.ToListAsync(cancellationToken);
        _db.OutboxMessages.RemoveRange(messages);
        await _db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
