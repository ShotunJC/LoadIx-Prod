using System.Diagnostics;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LoadIx.Infrastructure.Identity;

public sealed class SecurityAuditWriter(
    LoadIxDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SecurityAuditWriter> logger,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly Action<ILogger, string, string, string, Exception?> AuditWriteFailed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(4101, "SecurityAuditWriteFailed"),
            "Security audit write failed for {EventType} with outcome {Outcome}; provider error {ExceptionType}.");

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
        string? idempotencyKeyHash = null,
        string? requestMetadataJson = null,
        string? beforeSummaryJson = null,
        string? afterSummaryJson = null,
        string? reason = null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var safeIpAddress = AuditSummary.SanitizeText(
            ipAddress ?? httpContext?.Connection.RemoteIpAddress?.ToString(),
            64) ?? string.Empty;
        var safeCorrelationId = AuditSummary.SanitizeText(
            correlationId ?? httpContext?.TraceIdentifier ?? Activity.Current?.Id,
            128);
        var safeTraceId = AuditSummary.SanitizeText(
            traceId ?? Activity.Current?.TraceId.ToString(),
            200);

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
            IpAddress = safeIpAddress,
            CorrelationId = safeCorrelationId,
            FailureCode = AuditSummary.SanitizeText(failureCode, 160),
            TraceId = safeTraceId,
            NonceHash = AuditSummary.SanitizeText(nonceHash, 64),
            IdempotencyKeyHash = AuditSummary.SanitizeText(idempotencyKeyHash, 64),
            RequestMetadataJson = AuditSummary.SanitizeJson(
                requestMetadataJson ?? CreateRequestMetadata(httpContext, safeIpAddress)),
            BeforeSummaryJson = AuditSummary.SanitizeJson(beforeSummaryJson),
            AfterSummaryJson = AuditSummary.SanitizeJson(afterSummaryJson),
            Reason = AuditSummary.SanitizeText(reason),
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
        string? idempotencyKeyHash = null,
        string? requestMetadataJson = null,
        string? beforeSummaryJson = null,
        string? afterSummaryJson = null,
        string? reason = null)
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
                idempotencyKeyHash,
                requestMetadataJson,
                beforeSummaryJson,
                afterSummaryJson,
                reason);
        }
        catch (Exception exception)
        {
            AuditWriteFailed(logger, eventType, outcome, exception.GetType().Name, null);
        }
    }

    private static string? CreateRequestMetadata(
        HttpContext? httpContext,
        string safeIpAddress)
    {
        if (httpContext is null)
        {
            return null;
        }

        return AuditSummary.Serialize(new
        {
            method = httpContext.Request.Method,
            path = httpContext.Request.Path.Value,
            contentType = httpContext.Request.ContentType,
            userAgent = AuditSummary.SanitizeText(
                httpContext.Request.Headers.UserAgent.ToString(),
                300),
            remoteIp = safeIpAddress,
        });
    }
}
