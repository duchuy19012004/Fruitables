namespace Fruitables.Services.Infrastructure.Auditing;

public interface IAuditLogWriter
{
    Task WriteAsync(
        string action,
        string entityType,
        int entityId,
        int changedByAdminId,
        string? oldValue = null,
        string? newValue = null,
        CancellationToken cancellationToken = default);
}
