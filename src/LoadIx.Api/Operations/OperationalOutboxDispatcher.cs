using LoadIx.Infrastructure.Operations;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Operations;

public sealed class OperationalOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<OperationsHub> hubContext,
    TimeProvider timeProvider,
    ILogger<OperationalOutboxDispatcher> logger) : BackgroundService
{
    private static readonly Action<ILogger, Guid, string, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(1, "OperationalEventDeliveryFailed"),
            "Operational event {EventId} delivery failed with {ExceptionType}");
    private static readonly Action<ILogger, string, Exception?> DispatchCycleFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "OperationalDispatchCycleFailed"),
            "Operational event dispatch cycle failed with {ExceptionType}");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                DispatchCycleFailed(logger, exception.GetType().Name, null);
            }
        }
    }

    internal async Task DispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var messages = await db.OperationalOutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.DispatchedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc).ThenBy(message => message.Id)
            .Take(50)
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await hubContext.Clients.Group(OperationsHub.TenantGroup(message.TenantId.Value))
                    .SendAsync("operations.event", new
                    {
                        id = message.Id,
                        type = message.EventType,
                        tripId = message.TripId,
                        occurredAtUtc = message.OccurredAtUtc,
                        payload = message.Payload,
                    }, cancellationToken);
                message.DispatchedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
                message.Attempts++;
            }
            catch (Exception exception)
            {
                message.Attempts++;
                message.LastError = "Operational event delivery failed.";
                DeliveryFailed(logger, message.Id, exception.GetType().Name, null);
            }
        }
        if (messages.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
