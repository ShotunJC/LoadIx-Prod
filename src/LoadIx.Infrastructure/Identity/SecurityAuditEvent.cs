using LoadIx.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoadIx.Infrastructure.Identity;

public sealed class SecurityAuditEvent
{
    public long Id { get; set; }

    public TenantId? TenantId { get; set; }

    public Guid? ActorUserId { get; set; }

    public Guid? ActorDeviceId { get; set; }

    public Guid? ActorCredentialId { get; set; }

    public string EventType { get; set; } = string.Empty;

    [NotMapped]
    public string Action
    {
        get => EventType;
        set => EventType = value;
    }

    public string Outcome { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    [NotMapped]
    public string EntityType
    {
        get => TargetType;
        set => TargetType = value;
    }

    public string? TargetId { get; set; }

    [NotMapped]
    public string? EntityIdentifier
    {
        get => TargetId;
        set => TargetId = value;
    }

    public bool IsSupportContext { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string? FailureCode { get; set; }

    public string? TraceId { get; set; }

    public string? NonceHash { get; set; }

    public string? IdempotencyKeyHash { get; set; }

    public string? RequestMetadataJson { get; set; }

    public string? BeforeSummaryJson { get; set; }

    public string? AfterSummaryJson { get; set; }

    public string? Reason { get; set; }
}
