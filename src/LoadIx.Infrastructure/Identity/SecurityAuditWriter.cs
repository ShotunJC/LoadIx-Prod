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
        CancellationToken cancellationToken)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Outcome = outcome,
            TargetType = targetType,
            TargetId = targetId,
            IsSupportContext = isSupportContext,
            OccurredAtUtc = timeProvider.GetUtcNow(),
            IpAddress = ipAddress ?? string.Empty,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
