using Fruitables.Data;
using Fruitables.Models;
using System.Threading;

namespace Fruitables.Services.Infrastructure.Auditing;

public sealed class AuditLogWriter : IAuditLogWriter
{
    private static long _sourceSequence = DateTime.UtcNow.Ticks;
    private readonly ApplicationDbContext _db;

    public AuditLogWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string action,
        string entityType,
        int entityId,
        int changedByAdminId,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            SourceType = entityType,
            SourceId = Interlocked.Increment(ref _sourceSequence),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangedByAdminId = changedByAdminId,
            ChangedAt = DateTime.UtcNow,
            OldValue = oldValue,
            NewValue = newValue
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
