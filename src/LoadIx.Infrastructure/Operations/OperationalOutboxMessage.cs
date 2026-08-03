using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Operations;

/// <summary>Durable, tenant-scoped operational notification recorded in the same transaction as its trip change.</summary>
public sealed class OperationalOutboxMessage
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid TripId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
