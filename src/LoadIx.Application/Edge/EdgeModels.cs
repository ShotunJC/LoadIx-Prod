using LoadIx.Domain.Common;

namespace LoadIx.Application.Edge;

public sealed record EdgeRequestEnvelope(
    string IdempotencyKey,
    string Nonce,
    DateTimeOffset TimestampUtc,
    string? ClientIpAddress);

public sealed record EdgeDeviceQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Sort = "name",
    string Direction = "asc");

public sealed record EdgeDeviceListResponse(
    IReadOnlyList<EdgeDeviceListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record EdgeDeviceListItem(
    Guid Id,
    string Name,
    string Status,
    Guid SiteId,
    string SiteName,
    string SiteCode,
    string Protocol,
    string ParserVersion,
    bool IsOnline,
    DateTimeOffset? LastHeartbeatAtUtc,
    string? LastAgentVersion,
    string? LastHeartbeatStatus,
    Guid? ActiveCredentialId,
    DateTimeOffset? CredentialCreatedAtUtc,
    string Version);

public sealed record EdgeSiteOption(Guid Id, string Name, string Code);

public sealed record EdgeRegistrationBootstrapModel(
    string Name,
    Guid SiteId,
    string WeighbridgeName,
    string WeighbridgeCode,
    string Protocol,
    string ParserVersion);

public sealed record EdgeRegistrationBootstrap(
    Guid DeviceId,
    Guid WeighbridgeDeviceId,
    string DeviceName,
    string SiteName,
    string BootstrapToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record EdgeRegistrationModel(
    Guid DeviceId,
    string BootstrapToken,
    string? AgentVersion);


public sealed record EdgeCredentialIssued(
    Guid DeviceId,
    Guid CredentialId,
    string CredentialType,
    string Secret,
    DateTimeOffset? ExpiresAtUtc);

public sealed record EdgeActionResponse(Guid DeviceId, string Status);

public sealed record EdgeHeartbeatModel(
    DateTimeOffset DeviceTimestampUtc,
    string? AgentVersion,
    string? Status,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record EdgeHeartbeatAck(
    Guid CommandId,
    DateTimeOffset ReceivedAtUtc,
    bool IsReplay);

public sealed record EdgeReadingModel(
    Guid WeighbridgeDeviceId,
    decimal DisplayedValue,
    WeightUnit Unit,
    bool IsStable,
    DateTimeOffset CapturedAtUtc,
    string? VehicleRegistration,
    string SerialMessageMetadata,
    string ParserVersion,
    string? CorrelationId);

public sealed record EdgeReadingReceipt(
    Guid CommandId,
    Guid RawReadingId,
    Guid? StableReadingId,
    Guid WeighbridgeDeviceId,
    decimal DisplayedValue,
    string Unit,
    bool IsStable,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string? VehicleRegistration,
    string ParserVersion,
    string CorrelationId,
    bool IsReplay);

public sealed record EdgeTicketModel(
    Guid WeighbridgeDeviceId,
    string TicketNumber,
    DateTimeOffset CapturedAtUtc,
    string Content,
    string? ContentType,
    IReadOnlyDictionary<string, string>? Metadata,
    string? CorrelationId);

public sealed record EdgeTicketReceipt(
    Guid CommandId,
    Guid TicketId,
    Guid WeighbridgeDeviceId,
    string TicketNumber,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string CorrelationId,
    bool IsReplay);

public sealed record EdgeConfiguration(
    Guid DeviceId,
    string DeviceName,
    Guid SiteId,
    string SiteName,
    string SiteCode,
    string Protocol,
    string ParserVersion,
    int HeartbeatIntervalSeconds,
    DateTimeOffset ServerTimeUtc,
    IReadOnlyList<EdgeConfigurationWeighbridge> Weighbridges);

public sealed record EdgeConfigurationWeighbridge(
    Guid Id,
    string Name,
    string Code,
    Guid SiteId,
    string Protocol,
    string ParserVersion,
    bool IsActive);

public sealed record EdgeDiagnosticStatus(
    Guid DeviceId,
    string Status,
    DateTimeOffset ServerTimeUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    int PendingCommandCount,
    int ReadingCount,
    int TicketCount);

public sealed record EdgeDeviceDiagnostics(
    Guid DeviceId,
    string DeviceName,
    string Status,
    DateTimeOffset? LastHeartbeatAtUtc,
    IReadOnlyList<EdgeHeartbeatItem> Heartbeats,
    IReadOnlyList<EdgeCommandItem> Commands,
    IReadOnlyList<EdgeReadingItem> Readings,
    IReadOnlyList<EdgeAuditItem> AuditEvents);

public sealed record EdgeHeartbeatItem(
    Guid Id,
    DateTimeOffset DeviceTimestampUtc,
    DateTimeOffset ReceivedAtUtc,
    string? AgentVersion,
    string? Status,
    string? Metadata);

public sealed record EdgeCommandItem(
    Guid Id,
    string CommandType,
    string Status,
    string IdempotencyKey,
    string Nonce,
    string? CorrelationId,
    DateTimeOffset DeviceTimestampUtc,
    DateTimeOffset ReceivedAtUtc,
    Guid? ResourceId,
    string? FailureCode);

public sealed record EdgeReadingItem(
    Guid Id,
    Guid WeighbridgeDeviceId,
    decimal DisplayedValue,
    string Unit,
    bool IsStable,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string? VehicleRegistration,
    string ParserVersion,
    string CorrelationId);

public sealed record EdgeAuditItem(
    long Id,
    string EventType,
    string Outcome,
    string? TargetId,
    Guid? ActorDeviceId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId);

public sealed record EdgeLiveReading(
    EdgeReadingItem? LatestReading,
    EdgeReadingItem? LatestStableReading,
    DateTimeOffset ServerTimeUtc);

public enum EdgeFailure
{
    None,
    Unauthorized,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

#pragma warning disable CA1000 // Matches the established typed-result API.
public sealed record EdgeResult<T>(
    T? Value,
    EdgeFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == EdgeFailure.None;

    public static EdgeResult<T> Success(T value) =>
        new(value, EdgeFailure.None, string.Empty, string.Empty);

    public static EdgeResult<T> Fail(
        EdgeFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
#pragma warning restore CA1000
