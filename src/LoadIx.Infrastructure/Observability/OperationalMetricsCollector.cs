using System.Diagnostics;
using LoadIx.Domain.Edge;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Operations;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoadIx.Infrastructure.Observability;

public sealed class OperationalMetricsCollector(
    IServiceScopeFactory scopeFactory,
    IOptions<EdgeDeviceOptions> edgeOptions,
    TimeProvider timeProvider,
    ILogger<OperationalMetricsCollector> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> CollectionFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1602, "OperationalMetricsCollectionFailed"),
            "Operational metrics collection failed with {ExceptionType}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            await CollectAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CollectAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CollectAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var offlineAfter = now.AddSeconds(-edgeOptions.Value.OfflineAfterSeconds);
            using var activity = LoadIxTelemetry.ActivitySource.StartActivity(
                "loadix.metrics.collect",
                System.Diagnostics.ActivityKind.Internal);

            var activeTrips = await db.Trips
                .IgnoreQueryFilters()
                .CountAsync(
                    trip => trip.Status != TripStatus.Completed
                        && trip.Status != TripStatus.Cancelled
                        && trip.Status != TripStatus.Rejected,
                    cancellationToken);
            var onlineEdgeDevices = await db.EdgeDevices
                .IgnoreQueryFilters()
                .CountAsync(
                    device => device.Status == EdgeDeviceStatus.Active
                        && device.LastHeartbeatAtUtc >= offlineAfter,
                    cancellationToken);
            var notificationQueue = await db.NotificationDeliveries
                .IgnoreQueryFilters()
                .CountAsync(
                    delivery => delivery.Status == "Pending"
                        && delivery.NextAttemptAtUtc <= now,
                    cancellationToken);
            var outboxQueue = await db.OperationalOutboxMessages
                .IgnoreQueryFilters()
                .CountAsync(
                    message => message.DispatchedAtUtc == null
                        && message.DeadLetteredAtUtc == null,
                    cancellationToken);
            var deadLetters = await db.OperationalOutboxMessages
                .IgnoreQueryFilters()
                .CountAsync(message => message.DeadLetteredAtUtc != null, cancellationToken);
            var reportQueue = await db.ReportExportJobs
                .IgnoreQueryFilters()
                .CountAsync(
                    job => job.Status == ReportExportJobStatus.Queued
                        || job.Status == ReportExportJobStatus.Processing,
                    cancellationToken);

            LoadIxTelemetry.SetWorkloadState(
                activeTrips,
                onlineEdgeDevices,
                notificationQueue,
                outboxQueue,
                deadLetters,
                reportQueue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LoadIxTelemetry.RecordJobFailure("metrics-collector");
            CollectionFailed(logger, exception.GetType().Name, null);
        }
        finally
        {
            LoadIxTelemetry.RecordJobDuration(
                "metrics-collector",
                Stopwatch.GetElapsedTime(started));
        }
    }
}
