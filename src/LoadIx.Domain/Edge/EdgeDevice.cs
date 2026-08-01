using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class EdgeDevice : AggregateRoot<Guid>, ITenantOwned
{
    private EdgeDevice(
        Guid id,
        TenantId tenantId,
        string name,
        Guid siteId,
        string protocol,
        string parserVersion)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        SiteId = siteId;
        Protocol = protocol;
        ParserVersion = parserVersion;
        Status = EdgeDeviceStatus.PendingRegistration;
    }

    private EdgeDevice()
    {
        Name = string.Empty;
        Protocol = string.Empty;
        ParserVersion = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public Guid SiteId { get; private set; }

    public string Protocol { get; private set; }

    public string ParserVersion { get; private set; }

    public EdgeDeviceStatus Status { get; private set; }

    public DateTimeOffset? RegisteredAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; private set; }

    public string? LastAgentVersion { get; private set; }

    public string? LastHeartbeatStatus { get; private set; }

    public static Result<EdgeDevice> Create(
        Guid id,
        TenantId tenantId,
        string name,
        Guid siteId,
        string protocol,
        string parserVersion)
    {
        var error = DomainValidation.Required(name, "edge_device.name", 160)
            ?? DomainValidation.Required(protocol, "edge_device.protocol", 80)
            ?? DomainValidation.Required(
                parserVersion,
                "edge_device.parser_version",
                80);
        if (id == Guid.Empty || tenantId.IsEmpty || siteId == Guid.Empty)
        {
            error = new DomainError(
                "edge_device.identifiers_required",
                "Edge device, tenant, and site identifiers are required.");
        }

        return error is not null
            ? Result<EdgeDevice>.Failure(error)
            : Result<EdgeDevice>.Success(new EdgeDevice(
                id,
                tenantId,
                name.Trim(),
                siteId,
                protocol.Trim(),
                parserVersion.Trim()));
    }

    public Result Register(DateTimeOffset registeredAtUtc, string? agentVersion)
    {
        if (Status == EdgeDeviceStatus.Revoked)
        {
            return Result.Failure(new DomainError(
                "edge_device.revoked",
                "A revoked edge device cannot be registered."));
        }

        Status = EdgeDeviceStatus.Active;
        RegisteredAtUtc = registeredAtUtc.ToUniversalTime();
        RevokedAtUtc = null;
        LastAgentVersion = DomainValidation.Optional(agentVersion);
        return Result.Success();
    }

    public Result RecordHeartbeat(
        DateTimeOffset receivedAtUtc,
        string? agentVersion,
        string? heartbeatStatus)
    {
        if (Status != EdgeDeviceStatus.Active)
        {
            return Result.Failure(new DomainError(
                "edge_device.not_active",
                "The edge device is not active."));
        }

        LastHeartbeatAtUtc = receivedAtUtc.ToUniversalTime();
        LastAgentVersion = DomainValidation.Optional(agentVersion);
        LastHeartbeatStatus = DomainValidation.Optional(heartbeatStatus);
        return Result.Success();
    }

    public Result Revoke(DateTimeOffset revokedAtUtc)
    {
        if (Status == EdgeDeviceStatus.Revoked)
        {
            return Result.Success();
        }

        Status = EdgeDeviceStatus.Revoked;
        RevokedAtUtc = revokedAtUtc.ToUniversalTime();
        return Result.Success();
    }
}
