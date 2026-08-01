using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class DeviceHeartbeat : Entity<Guid>, ITenantOwned
{
    private DeviceHeartbeat(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        DateTimeOffset deviceTimestampUtc,
        DateTimeOffset receivedAtUtc,
        string? agentVersion,
        string? status,
        string? metadataJson)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        DeviceTimestampUtc = deviceTimestampUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        AgentVersion = DomainValidation.Optional(agentVersion);
        Status = DomainValidation.Optional(status);
        MetadataJson = DomainValidation.Optional(metadataJson);
    }

    private DeviceHeartbeat()
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public DateTimeOffset DeviceTimestampUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string? AgentVersion { get; private set; }

    public string? Status { get; private set; }

    public string? MetadataJson { get; private set; }

    public static Result<DeviceHeartbeat> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        DateTimeOffset deviceTimestampUtc,
        DateTimeOffset receivedAtUtc,
        string? agentVersion,
        string? status,
        string? metadataJson)
    {
        var error = agentVersion?.Trim().Length > 80
            ? new DomainError(
                "heartbeat.agent_version_too_long",
                "Agent version cannot exceed 80 characters.")
            : status?.Trim().Length > 40
                ? new DomainError(
                    "heartbeat.status_too_long",
                    "Heartbeat status cannot exceed 40 characters.")
                : metadataJson?.Length > 4000
                    ? new DomainError(
                        "heartbeat.metadata_too_long",
                        "Heartbeat metadata cannot exceed 4000 characters.")
                    : null;
        if (id == Guid.Empty || tenantId.IsEmpty || edgeDeviceId == Guid.Empty)
        {
            error = new DomainError(
                "heartbeat.identifiers_required",
                "Heartbeat, tenant, and edge device identifiers are required.");
        }

        return error is not null
            ? Result<DeviceHeartbeat>.Failure(error)
            : Result<DeviceHeartbeat>.Success(new DeviceHeartbeat(
                id,
                tenantId,
                edgeDeviceId,
                deviceTimestampUtc,
                receivedAtUtc,
                agentVersion,
                status,
                metadataJson));
    }
}
