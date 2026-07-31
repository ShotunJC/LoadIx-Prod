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
    private static readonly Action<ILogger, Guid, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, "OperationalEventDeliveryFailed"),
            "Operational event {EventId} delivery failed");
    private static readonly Action<ILogger, Exception?> DispatchCycleFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, "OperationalDispatchCycleFailed"),
            "Operational event dispatch cycle failed");
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
                DispatchCycleFailed(logger, exception);
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
                message.LastError = exception.Message[..Math.Min(exception.Message.Length, 1000)];
                DeliveryFailed(logger, message.Id, exception);
            }
        }
        if (messages.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
