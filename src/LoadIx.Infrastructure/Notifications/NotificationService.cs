using LoadIx.Application.Abstractions;
using LoadIx.Application.Notifications;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Notifications;

public sealed class NotificationService(LoadIxDbContext dbContext, ICurrentRequestContext requestContext, TimeProvider timeProvider) : INotificationService
{
    public async Task QueueAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (requestContext.TenantId is null) throw new InvalidOperationException("A trusted tenant context is required for notifications.");
        if (!NotificationTypes.All.Contains(request.Type, StringComparer.Ordinal))
            throw new InvalidOperationException($"Notification type '{request.Type}' is not registered.");
        var recipientTenant = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == request.RecipientUserId)
            .Select(user => user.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (recipientTenant != requestContext.TenantId.Value)
            throw new InvalidOperationException("Notification recipient is outside the trusted tenant context.");
        if (await dbContext.NotificationMessages.AnyAsync(item => item.RecipientUserId == request.RecipientUserId && item.DeduplicationKey == request.DeduplicationKey, cancellationToken)) return;
        var preference = await dbContext.UserNotificationPreferences.SingleOrDefaultAsync(item => item.UserId == request.RecipientUserId && item.NotificationType == request.Type, cancellationToken);
        var timestamp = timeProvider.GetUtcNow().ToUniversalTime();
        var message = new NotificationMessage { Id = Guid.NewGuid(), TenantId = requestContext.TenantId.Value, RecipientUserId = request.RecipientUserId, Type = request.Type, Title = request.Title, Body = request.Body, DeduplicationKey = request.DeduplicationKey, CreatedAtUtc = timestamp };
        dbContext.NotificationMessages.Add(message);
        foreach (var channel in (request.Channels ?? ["in-app", "email"]).Distinct(StringComparer.Ordinal).Where(channel => IsEnabled(channel, preference))) dbContext.NotificationDeliveries.Add(new NotificationDelivery { Id = Guid.NewGuid(), NotificationId = message.Id, TenantId = message.TenantId, Channel = channel, Status = "Pending", NextAttemptAtUtc = timestamp });
    }
    private static bool IsEnabled(string channel, UserNotificationPreference? preference) => channel switch { "in-app" => preference?.InAppEnabled ?? true, "email" => preference?.EmailEnabled ?? true, "sms" => preference?.SmsEnabled ?? false, _ => false };
}
