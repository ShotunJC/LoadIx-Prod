namespace LoadIx.Contracts.Edge;

public sealed record CreateEdgeDeviceBootstrapRequest(
    string Name,
    Guid SiteId,
    string WeighbridgeName,
    string WeighbridgeCode,
    string Protocol,
    string ParserVersion);

public sealed record RegisterEdgeDeviceRequest(
    Guid DeviceId,
    string BootstrapToken,
    string? AgentVersion);

public sealed record EdgeHeartbeatRequest(
    DateTimeOffset DeviceTimestampUtc,
    string? AgentVersion,
    string? Status,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record EdgeWeightReadingRequest(
    Guid WeighbridgeDeviceId,
    decimal DisplayedValue,
    string Unit,
    bool IsStable,
    DateTimeOffset CapturedAtUtc,
    string? VehicleRegistration,
    string SerialMessageMetadata,
    string ParserVersion,
    string? CorrelationId);

public sealed record EdgeTicketSubmissionRequest(
    Guid WeighbridgeDeviceId,
    string TicketNumber,
    DateTimeOffset CapturedAtUtc,
    string Content,
    string? ContentType,
    IReadOnlyDictionary<string, string>? Metadata,
    string? CorrelationId);
