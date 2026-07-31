using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class ContractRoute : Entity<Guid>, ITenantOwned
{
    private ContractRoute(
        Guid id,
        TenantId tenantId,
        Guid sourceLocationId,
        Guid destinationLocationId,
        string reference)
        : base(id)
    {
        TenantId = tenantId;
        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        Reference = reference;
    }

    private ContractRoute()
    {
        Reference = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid TransportContractId { get; private set; }

    public Guid SourceLocationId { get; private set; }

    public Guid DestinationLocationId { get; private set; }

    public string Reference { get; private set; }

    internal static Result<ContractRoute> Create(
        Guid id,
        TenantId tenantId,
        Guid sourceLocationId,
        Guid destinationLocationId,
        string reference)
    {
        var error = DomainValidation.Required(reference, "contract.route_reference", 80);
        if (id == Guid.Empty || tenantId.IsEmpty
            || sourceLocationId == Guid.Empty || destinationLocationId == Guid.Empty)
        {
            error = new DomainError(
                "contract.route_identifiers_required",
                "Route, tenant, source, and destination identifiers are required.");
        }
        else if (sourceLocationId == destinationLocationId)
        {
            error = new DomainError(
                "contract.route_locations_distinct",
                "Source and destination locations must be different.");
        }

        return error is null
            ? Result<ContractRoute>.Success(new ContractRoute(
                id, tenantId, sourceLocationId, destinationLocationId,
                reference.Trim().ToUpperInvariant()))
            : Result<ContractRoute>.Failure(error);
    }
}

public sealed record ContractRouteDefinition(
    Guid? Id,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    string Reference);
