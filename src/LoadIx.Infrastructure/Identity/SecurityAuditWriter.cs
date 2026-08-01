using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace LoadIx.Infrastructure.Identity;

public sealed class SecurityAuditWriter(
    LoadIxDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SecurityAuditWriter> logger)
{
    private static readonly Action<ILogger, string, string, Exception?> AuditWriteFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4101, "SecurityAuditWriteFailed"),
            "Security audit write failed for {EventType} with outcome {Outcome}.");

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
        Guid? actorCredentialId = null,
        string? correlationId = null,
        string? failureCode = null,
        string? traceId = null,
        string? nonceHash = null,
        string? idempotencyKeyHash = null)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorDeviceId = actorDeviceId,
            ActorCredentialId = actorCredentialId,
            EventType = eventType,
            Outcome = outcome,
            TargetType = targetType,
            TargetId = targetId,
            IsSupportContext = isSupportContext,
            OccurredAtUtc = timeProvider.GetUtcNow(),
            IpAddress = ipAddress ?? string.Empty,
            CorrelationId = correlationId,
            FailureCode = failureCode,
            TraceId = traceId,
            NonceHash = nonceHash,
            IdempotencyKeyHash = idempotencyKeyHash,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TryWriteAsync(
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
        Guid? actorCredentialId = null,
        string? correlationId = null,
        string? failureCode = null,
        string? traceId = null,
        string? nonceHash = null,
        string? idempotencyKeyHash = null)
    {
        try
        {
            await WriteAsync(
                eventType,
                outcome,
                targetType,
                targetId,
                tenantId,
                actorUserId,
                isSupportContext,
                ipAddress,
                cancellationToken,
                actorDeviceId,
                actorCredentialId,
                correlationId,
                failureCode,
                traceId,
                nonceHash,
                idempotencyKeyHash);
        }
        catch (Exception exception)
        {
            AuditWriteFailed(logger, eventType, outcome, exception);
        }
    }
}
