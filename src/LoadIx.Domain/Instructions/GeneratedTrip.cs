using LoadIx.Domain.Common;

namespace LoadIx.Domain.Instructions;

public sealed class GeneratedTrip : Entity<Guid>, ITenantOwned
{
    private GeneratedTrip(
        Guid id,
        TenantId tenantId,
        string tripNumber,
        decimal plannedQuantity,
        WeightUnit quantityUnit)
        : base(id)
    {
        TenantId = tenantId;
        TripNumber = tripNumber;
        PlannedQuantity = plannedQuantity;
        QuantityUnit = quantityUnit;
        Status = GeneratedTripStatus.Planned;
    }

    private GeneratedTrip()
    {
        TripNumber = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid LoadInstructionId { get; private set; }

    public string TripNumber { get; private set; }

    public decimal PlannedQuantity { get; private set; }

    public decimal? CompletedQuantity { get; private set; }

    public WeightUnit QuantityUnit { get; private set; }

    public GeneratedTripStatus Status { get; private set; }

    internal static Result<GeneratedTrip> Create(
        Guid id,
        TenantId tenantId,
        string tripNumber,
        decimal plannedQuantity,
        WeightUnit quantityUnit)
    {
        var error = DomainValidation.Required(tripNumber, "trip.number", 80);
        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "trip.identifiers_required",
                "Trip and tenant identifiers are required.");
        }
        else if (plannedQuantity <= 0)
        {
            error = new DomainError(
                "trip.planned_quantity_invalid",
                "A generated trip must have a positive planned quantity.");
        }
        else if (!Enum.IsDefined(quantityUnit))
        {
            error = new DomainError(
                "trip.quantity_unit_invalid",
                "The trip quantity unit is not supported.");
        }

        return error is null
            ? Result<GeneratedTrip>.Success(new GeneratedTrip(
                id,
                tenantId,
                tripNumber.Trim().ToUpperInvariant(),
                plannedQuantity,
                quantityUnit))
            : Result<GeneratedTrip>.Failure(error);
    }

    internal Result Complete(decimal completedQuantity)
    {
        if (Status != GeneratedTripStatus.Planned)
        {
            return Result.Failure(new DomainError(
                "trip.completion_status_invalid",
                "Only a planned trip can be completed."));
        }

        if (completedQuantity <= 0)
        {
            return Result.Failure(new DomainError(
                "trip.completed_quantity_invalid",
                "Completed trip quantity must be positive."));
        }

        CompletedQuantity = decimal.Round(
            completedQuantity,
            3,
            MidpointRounding.AwayFromZero);
        Status = GeneratedTripStatus.Completed;
        return Result.Success();
    }

    internal void Cancel()
    {
        if (Status == GeneratedTripStatus.Planned)
        {
            Status = GeneratedTripStatus.Cancelled;
        }
    }
}
