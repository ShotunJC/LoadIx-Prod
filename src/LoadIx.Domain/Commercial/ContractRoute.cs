using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class ContractRoute : Entity<Guid>, ITenantOwned
{
    private ContractRoute(
        Guid id,
        TenantId tenantId,
        Guid sourceLocationId,
        Guid destinationLocationId,
        string reference,
        bool sourceWeightCaptureRequired,
        bool destinationWeightCaptureRequired,
        string? collectionInstructions,
        string? deliveryInstructions)
        : base(id)
    {
        TenantId = tenantId;
        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        Reference = reference;
        SourceWeightCaptureRequired = sourceWeightCaptureRequired;
        DestinationWeightCaptureRequired = destinationWeightCaptureRequired;
        CollectionInstructions = collectionInstructions;
        DeliveryInstructions = deliveryInstructions;
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

    public bool SourceWeightCaptureRequired { get; private set; }

    public bool DestinationWeightCaptureRequired { get; private set; }

    public string? CollectionInstructions { get; private set; }

    public string? DeliveryInstructions { get; private set; }

    internal static Result<ContractRoute> Create(
        Guid id,
        TenantId tenantId,
        Guid sourceLocationId,
        Guid destinationLocationId,
        string reference,
        bool sourceWeightCaptureRequired = true,
        bool destinationWeightCaptureRequired = true,
        string? collectionInstructions = null,
        string? deliveryInstructions = null)
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

        if (collectionInstructions?.Trim().Length > 1000
            || deliveryInstructions?.Trim().Length > 1000)
        {
            error = new DomainError(
                "contract.route_instructions_too_long",
                "Collection and delivery instructions cannot exceed 1000 characters.");
        }

        return error is null
            ? Result<ContractRoute>.Success(new ContractRoute(
                id, tenantId, sourceLocationId, destinationLocationId,
                reference.Trim().ToUpperInvariant(),
                sourceWeightCaptureRequired,
                destinationWeightCaptureRequired,
                string.IsNullOrWhiteSpace(collectionInstructions)
                    ? null : collectionInstructions.Trim(),
                string.IsNullOrWhiteSpace(deliveryInstructions)
                    ? null : deliveryInstructions.Trim()))
            : Result<ContractRoute>.Failure(error);
    }

    internal Result Update(ContractRouteDefinition definition)
    {
        var candidate = Create(
            Id, TenantId, definition.SourceLocationId,
            definition.DestinationLocationId, definition.Reference,
            definition.SourceWeightCaptureRequired,
            definition.DestinationWeightCaptureRequired,
            definition.CollectionInstructions, definition.DeliveryInstructions);
        if (candidate.IsFailure) return Result.Failure(candidate.Error!);
        SourceLocationId = candidate.Value.SourceLocationId;
        DestinationLocationId = candidate.Value.DestinationLocationId;
        Reference = candidate.Value.Reference;
        SourceWeightCaptureRequired = candidate.Value.SourceWeightCaptureRequired;
        DestinationWeightCaptureRequired = candidate.Value.DestinationWeightCaptureRequired;
        CollectionInstructions = candidate.Value.CollectionInstructions;
        DeliveryInstructions = candidate.Value.DeliveryInstructions;
        return Result.Success();
    }
}

public sealed record ContractRouteDefinition(
    Guid? Id,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    string Reference,
    bool SourceWeightCaptureRequired = true,
    bool DestinationWeightCaptureRequired = true,
    string? CollectionInstructions = null,
    string? DeliveryInstructions = null);
