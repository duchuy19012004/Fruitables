using System.ComponentModel.DataAnnotations;

namespace Fruitables.Models;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)] public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    [MaxLength(200)] public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    [MaxLength(4000)] public string? LastError { get; set; }
    public DateTime? DeadLetteredAtUtc { get; set; }
    [MaxLength(64)] public string? LockToken { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
