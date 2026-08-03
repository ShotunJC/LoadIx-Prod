using System.Diagnostics;
using LoadIx.Application.Notifications;
using LoadIx.Application.Security;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Observability;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Resilience;
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

    private static readonly Action<ILogger, Guid, string, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(1, "NotificationDeliveryFailed"),
            "Notification delivery {DeliveryId} failed with {ExceptionType}");

    private static readonly Action<ILogger, string, Exception?> DispatchCycleFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "NotificationDispatchCycleFailed"),
            "Notification delivery dispatch cycle failed with {ExceptionType}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    await DispatchAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    LoadIxTelemetry.RecordJobFailure("notification-dispatcher");
                    DispatchCycleFailed(logger, exception.GetType().Name, null);
                }
                finally
                {
                    LoadIxTelemetry.RecordJobDuration(
                        "notification-dispatcher",
                        Stopwatch.GetElapsedTime(started));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
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

        if (deliveries.Count == 0)
        {
            return;
        }

        var notificationIds = deliveries
            .Select(delivery => delivery.NotificationId)
            .Distinct()
            .ToArray();
        var messages = await db.NotificationMessages
            .IgnoreQueryFilters()
            .Where(message => notificationIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken);
        var recipientIds = messages.Values
            .Select(message => message.RecipientUserId)
            .Distinct()
            .ToArray();
        var users = await db.Users
            .IgnoreQueryFilters()
            .Where(user => recipientIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
        var tenantIds = messages.Values.Select(message => message.TenantId).Distinct().ToArray();
        var types = messages.Values.Select(message => message.Type).Distinct().ToArray();
        var channels = deliveries.Select(delivery => delivery.Channel).Distinct().ToArray();
        var templates = await db.TenantNotificationTemplates
            .IgnoreQueryFilters()
            .Where(template => tenantIds.Contains(template.TenantId)
                && types.Contains(template.NotificationType)
                && channels.Contains(template.Channel))
            .ToDictionaryAsync(
                template => (template.TenantId, template.NotificationType, template.Channel),
                cancellationToken);

        foreach (var delivery in deliveries)
        {
            if (!messages.TryGetValue(delivery.NotificationId, out var message))
            {
                ApplyResult(
                    db,
                    delivery,
                    null,
                    NotificationSendResult.Failed("The notification message is unavailable."),
                    now);
                continue;
            }

            users.TryGetValue(message.RecipientUserId, out var user);
            templates.TryGetValue(
                (message.TenantId, message.Type, delivery.Channel),
                out var template);

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

        await db.SaveChangesAsync(cancellationToken);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DeliveryFailed(logger, delivery.Id, exception.GetType().Name, null);
            return NotificationSendResult.Failed(
                "The notification provider failed.",
                TransientOperationExecutor.IsTransient(exception)
                    || exception is ProviderCircuitOpenException);
        }
    }

    private static void ApplyResult(
        LoadIxDbContext db,
        NotificationDelivery delivery,
        NotificationMessage? message,
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
            if (message is not null)
            {
                AddAuditEvent(db, delivery, message, "Delivered", now);
            }
            return;
        }

        LoadIxTelemetry.RecordNotificationFailure(delivery.Channel);
        delivery.Failure = result.Failure?[..Math.Min(result.Failure.Length, 1000)];
        delivery.Status = !result.IsTransient || delivery.Attempts >= MaximumAttempts
            ? "Failed"
            : "Pending";
        delivery.NextAttemptAtUtc = delivery.Status == "Pending"
            ? now.AddSeconds(Math.Min(300, 2 << Math.Min(delivery.Attempts - 1, 7)))
            : now;
        if (message is not null)
        {
            AddAuditEvent(db, delivery, message, delivery.Status, now);
        }
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
