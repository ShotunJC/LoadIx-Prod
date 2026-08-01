using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class WeighbridgeDevice : AggregateRoot<Guid>, ITenantOwned
{
    private WeighbridgeDevice(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid siteId,
        string name,
        string code,
        string protocol,
        string parserVersion)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        SiteId = siteId;
        Name = name;
        Code = code;
        Protocol = protocol;
        ParserVersion = parserVersion;
        IsActive = true;
    }

    private WeighbridgeDevice()
    {
        Name = string.Empty;
        Code = string.Empty;
        Protocol = string.Empty;
        ParserVersion = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public Guid SiteId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public string Protocol { get; private set; }

    public string ParserVersion { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<WeighbridgeDevice> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid siteId,
        string name,
        string code,
        string protocol,
        string parserVersion)
    {
        var error = DomainValidation.Required(name, "weighbridge.name", 160)
            ?? DomainValidation.Required(code, "weighbridge.code", 40)
            ?? DomainValidation.Required(protocol, "weighbridge.protocol", 80)
            ?? DomainValidation.Required(
                parserVersion,
                "weighbridge.parser_version",
                80);
        if (id == Guid.Empty
            || tenantId.IsEmpty
            || edgeDeviceId == Guid.Empty
            || siteId == Guid.Empty)
        {
            error = new DomainError(
                "weighbridge.identifiers_required",
                "Weighbridge, tenant, edge device, and site identifiers are required.");
        }

        return error is not null
            ? Result<WeighbridgeDevice>.Failure(error)
            : Result<WeighbridgeDevice>.Success(new WeighbridgeDevice(
                id,
                tenantId,
                edgeDeviceId,
                siteId,
                name.Trim(),
                code.Trim().ToUpperInvariant(),
                protocol.Trim(),
                parserVersion.Trim()));
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
