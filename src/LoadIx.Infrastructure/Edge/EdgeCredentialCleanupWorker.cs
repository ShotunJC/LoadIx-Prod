using LoadIx.Application.Security;
using LoadIx.Domain.Edge;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoadIx.Infrastructure.Edge;

public sealed class EdgeCredentialCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EdgeDeviceOptions> options,
    TimeProvider timeProvider,
    ILogger<EdgeCredentialCleanupWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> CredentialsExpired =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4202, "EdgeCredentialCleanupCompleted"),
            "Expired {CredentialCount} stale edge credential(s) during scheduled cleanup.");

    private static readonly Action<ILogger, Exception?> CleanupFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(4203, "EdgeCredentialCleanupFailed"),
            "Scheduled edge credential cleanup failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            options.Value.CredentialCleanupIntervalSeconds);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, timeProvider, stoppingToken);
                await ExpireCredentialsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    private async Task ExpireCredentialsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            var requestContext = scope.ServiceProvider
                .GetRequiredService<CurrentRequestContext>();
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var tenantIds = await dbContext.EdgeDeviceCredentials
                .IgnoreQueryFilters()
                .Where(item => item.Type == EdgeDeviceCredentialType.Device
                    && item.Status == EdgeDeviceCredentialStatus.PendingConfirmation
                    && item.RevokedAtUtc == null
                    && item.OverlapExpiresAtUtc <= now)
                .Select(item => item.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var expiredCount = 0;
            foreach (var tenantId in tenantIds)
            {
                requestContext.Set(tenantId, null, false, LoadIxPermissions.All);
                var expired = await dbContext.EdgeDeviceCredentials
                    .IgnoreQueryFilters()
                    .Where(item => item.TenantId == tenantId
                        && item.Type == EdgeDeviceCredentialType.Device
                        && item.Status == EdgeDeviceCredentialStatus.PendingConfirmation
                        && item.RevokedAtUtc == null
                        && item.OverlapExpiresAtUtc <= now)
                    .ToListAsync(cancellationToken);
                foreach (var credential in expired)
                {
                    credential.Revoke(now);
                }

                if (expired.Count > 0)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    expiredCount += expired.Count;
                }
            }

            if (expiredCount > 0)
            {
                CredentialsExpired(logger, expiredCount, null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CleanupFailed(logger, exception);
        }
    }
}
