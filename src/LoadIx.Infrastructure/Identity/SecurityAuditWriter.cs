using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;

namespace LoadIx.Infrastructure.Identity;

public sealed class SecurityAuditWriter(
    LoadIxDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task WriteAsync(
        string eventType,
        string outcome,
        string targetType,
        string? targetId,
        TenantId? tenantId,
        Guid? actorUserId,
        bool isSupportContext,
        string? ipAddress,
        CancellationToken cancellationToken,
        Guid? actorDeviceId = null,
        string? correlationId = null)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorDeviceId = actorDeviceId,
            EventType = eventType,
            Outcome = outcome,
            TargetType = targetType,
            TargetId = targetId,
            IsSupportContext = isSupportContext,
            OccurredAtUtc = timeProvider.GetUtcNow(),
            IpAddress = ipAddress ?? string.Empty,
            CorrelationId = correlationId,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
