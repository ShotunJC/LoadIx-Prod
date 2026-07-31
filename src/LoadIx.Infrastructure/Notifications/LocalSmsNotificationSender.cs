using LoadIx.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LoadIx.Infrastructure.Notifications;

public sealed class LocalSmsNotificationSender(
    ILogger<LocalSmsNotificationSender> logger) : ISmsNotificationSender
{
    private static readonly Action<ILogger, Guid, string, Exception?> LocalSmsQueued =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(1, "LocalSmsNotificationQueued"),
            "Local SMS notification queued for user {RecipientUserId} with type {NotificationType}");

    public Task<NotificationSendResult> SendAsync(
        SmsNotificationMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            return Task.FromResult(NotificationSendResult.Failed(
                "The recipient does not have an SMS destination configured."));
        }

        LocalSmsQueued(
            logger,
            message.RecipientUserId,
            message.NotificationType,
            null);
        return Task.FromResult(NotificationSendResult.Success(
            $"local-sms-{Guid.NewGuid():N}"));
    }
}
