using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Identity;

public sealed class SecurityAuditEvent
{
    public long Id { get; set; }

    public TenantId? TenantId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public bool IsSupportContext { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string IpAddress { get; set; } = string.Empty;
}
