using LoadIx.Application.Notifications;
using LoadIx.Application.Security;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Operations;

public sealed class NotificationDeliveryDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<OperationsHub> hubContext,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryDispatcher> logger) : BackgroundService
{
    private const int MaximumAttempts = 5;

    private static readonly Action<ILogger, Guid, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, "NotificationDeliveryFailed"),
            "Notification delivery {DeliveryId} failed");

    private static readonly Action<ILogger, Exception?> DispatchCycleFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, "NotificationDispatchCycleFailed"),
            "Notification delivery dispatch cycle failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
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
        var emailSender = scope.ServiceProvider.GetRequiredService<IAccountEmailSender>();
        var smsSender = scope.ServiceProvider.GetRequiredService<ISmsNotificationSender>();
        var now = timeProvider.GetUtcNow().ToUniversalTime();

        var deliveries = await db.NotificationDeliveries
            .IgnoreQueryFilters()
            .Where(delivery => delivery.Status == "Pending"
                && delivery.NextAttemptAtUtc <= now)
            .OrderBy(delivery => delivery.NextAttemptAtUtc)
            .ThenBy(delivery => delivery.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            var message = await db.NotificationMessages
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == delivery.NotificationId, cancellationToken);
            var user = await db.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == message.RecipientUserId, cancellationToken);
            var template = await db.TenantNotificationTemplates
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == message.TenantId
                    && item.NotificationType == message.Type
                    && item.Channel == delivery.Channel,
                    cancellationToken);

            var subject = Render(template?.SubjectTemplate, message.Title, message);
            var body = Render(template?.BodyTemplate, message.Body, message);
            var result = await TrySendAsync(
                delivery,
                message,
                user,
                subject,
                body,
                emailSender,
                smsSender,
                cancellationToken);
            ApplyResult(db, delivery, message, result, now);
        }

        if (deliveries.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<NotificationSendResult> TrySendAsync(
        NotificationDelivery delivery,
        NotificationMessage message,
        ApplicationUser? user,
        string subject,
        string body,
        IAccountEmailSender emailSender,
        ISmsNotificationSender smsSender,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (delivery.Channel)
            {
                case "in-app":
                    await hubContext.Clients.Group(OperationsHub.UserGroup(message.RecipientUserId))
                        .SendAsync("notifications.event", new
                        {
                            message.Id,
                            message.Type,
                            message.Title,
                            message.Body,
                            message.CreatedAtUtc,
                        }, cancellationToken);
                    return NotificationSendResult.Success($"signalr-{message.Id:N}");
                case "email" when user?.Email is not null:
                    await emailSender.SendAsync(user.Email, subject, body, cancellationToken);
                    return NotificationSendResult.Success($"smtp-{Guid.NewGuid():N}");
                case "email":
                    return NotificationSendResult.Failed("The recipient does not have an email address.");
                case "sms":
                    return await smsSender.SendAsync(
                        new SmsNotificationMessage(
                            message.RecipientUserId,
                            user?.PhoneNumber,
                            body,
                            message.Type),
                        cancellationToken);
                default:
                    return NotificationSendResult.Failed(
                        $"Unsupported notification channel '{delivery.Channel}'.");
            }
        }
        catch (Exception exception)
        {
            DeliveryFailed(logger, delivery.Id, exception);
            return NotificationSendResult.Failed(exception.Message);
        }
    }

    private static void ApplyResult(
        LoadIxDbContext db,
        NotificationDelivery delivery,
        NotificationMessage message,
        NotificationSendResult result,
        DateTimeOffset now)
    {
        delivery.Attempts++;
        delivery.ProviderCorrelationId = result.ProviderCorrelationId;
        if (result.Succeeded)
        {
            delivery.Status = "Delivered";
            delivery.Failure = null;
            delivery.DeliveredAtUtc = now;
            AddAuditEvent(db, delivery, message, "Delivered", now);
            return;
        }

        delivery.Failure = result.Failure?[..Math.Min(result.Failure.Length, 1000)];
        delivery.Status = delivery.Attempts >= MaximumAttempts ? "Failed" : "Pending";
        delivery.NextAttemptAtUtc = now.AddMinutes(Math.Pow(2, delivery.Attempts));
        AddAuditEvent(db, delivery, message, delivery.Status, now);
    }

    private static string Render(
        string? template,
        string fallback,
        NotificationMessage message) =>
        string.IsNullOrWhiteSpace(template)
            ? fallback
            : template
                .Replace("{{Title}}", message.Title, StringComparison.Ordinal)
                .Replace("{{Body}}", message.Body, StringComparison.Ordinal)
                .Replace("{{Type}}", message.Type, StringComparison.Ordinal);

    private static void AddAuditEvent(
        LoadIxDbContext db,
        NotificationDelivery delivery,
        NotificationMessage message,
        string outcome,
        DateTimeOffset now)
    {
        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = delivery.TenantId,
            ActorUserId = null,
            EventType = $"Notification.{delivery.Channel}",
            Outcome = outcome,
            TargetType = "Notification",
            TargetId = message.Id.ToString(),
            IsSupportContext = false,
            OccurredAtUtc = now,
            IpAddress = string.Empty,
        });
    }
}
