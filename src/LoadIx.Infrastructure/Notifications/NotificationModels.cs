using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Notifications;

public sealed class NotificationMessage
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public TenantId TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string? ProviderCorrelationId { get; set; }
    public string? Failure { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
}

public sealed class UserNotificationPreference
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
}

public sealed class TenantNotificationTemplate
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public DateTimeOffset ModifiedAtUtc { get; set; }
}
