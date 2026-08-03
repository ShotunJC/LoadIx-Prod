namespace LoadIx.Application.Notifications;

public sealed record NotificationRequest(
    Guid RecipientUserId,
    string Type,
    string Title,
    string Body,
    string DeduplicationKey,
    IReadOnlyCollection<string>? Channels = null);

public interface INotificationService
{
    Task QueueAsync(NotificationRequest request, CancellationToken cancellationToken);
}

public sealed record SmsNotificationMessage(
    Guid RecipientUserId,
    string? PhoneNumber,
    string Body,
    string NotificationType);

public sealed record NotificationSendResult(
    bool Succeeded,
    string? ProviderCorrelationId,
    string? Failure,
    bool IsTransient = false)
{
    public static NotificationSendResult Success(string providerCorrelationId) =>
        new(true, providerCorrelationId, null);

    public static NotificationSendResult Failed(
        string failure,
        bool isTransient = false) =>
        new(false, null, failure, isTransient);
}

public interface ISmsNotificationSender
{
    Task<NotificationSendResult> SendAsync(
        SmsNotificationMessage message,
        CancellationToken cancellationToken);
}

public static class NotificationTypes
{
    public const string UserInvitation = "UserInvitation";
    public const string PasswordReset = "PasswordReset";
    public const string TripAssignment = "TripAssignment";
    public const string Dispatch = "Dispatch";
    public const string CollectionArrival = "CollectionArrival";
    public const string DeliveryArrival = "DeliveryArrival";
    public const string ReconciliationException = "ReconciliationException";
    public const string DocumentMissing = "DocumentMissing";
    public const string LicenceOrRoadworthyExpiryWarning = "LicenceOrRoadworthyExpiryWarning";
    public const string DelayedTrip = "DelayedTrip";
    public const string TripCompletion = "TripCompletion";
    public const string InvoiceBatchReady = "InvoiceBatchReady";

    public static IReadOnlyList<string> All { get; } =
    [
        UserInvitation,
        PasswordReset,
        TripAssignment,
        Dispatch,
        CollectionArrival,
        DeliveryArrival,
        ReconciliationException,
        DocumentMissing,
        LicenceOrRoadworthyExpiryWarning,
        DelayedTrip,
        TripCompletion,
        InvoiceBatchReady,
    ];
}
