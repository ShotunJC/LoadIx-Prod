using LoadIx.Domain.Common;

namespace LoadIx.Domain.Instructions;

public sealed class LoadInstruction : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<GeneratedTrip> _trips = [];

    private LoadInstruction(
        Guid id,
        TenantId tenantId,
        LoadInstructionDefinition definition)
        : base(id)
    {
        InstructionNumber = string.Empty;
        CustomerPurchaseOrderReference = string.Empty;
        CustomerInstructionReference = string.Empty;
        SpecialInstructions = string.Empty;
        CancellationReason = string.Empty;
        TenantId = tenantId;
        Apply(definition);
        Status = LoadInstructionStatus.Draft;
    }

    private LoadInstruction()
    {
        InstructionNumber = string.Empty;
        CustomerPurchaseOrderReference = string.Empty;
        CustomerInstructionReference = string.Empty;
        SpecialInstructions = string.Empty;
        CancellationReason = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string InstructionNumber { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid TransportContractId { get; private set; }

    public Guid ContractRouteId { get; private set; }

    public Guid CommodityId { get; private set; }

    public decimal RequestedQuantity { get; private set; }

    public WeightUnit QuantityUnit { get; private set; }

    public int? RequestedTripCount { get; private set; }

    public DateTimeOffset CollectionWindowStartUtc { get; private set; }

    public DateTimeOffset CollectionWindowEndUtc { get; private set; }

    public DateTimeOffset DeliveryWindowStartUtc { get; private set; }

    public DateTimeOffset DeliveryWindowEndUtc { get; private set; }

    public string CustomerPurchaseOrderReference { get; private set; }

    public string CustomerInstructionReference { get; private set; }

    public LoadInstructionPriority Priority { get; private set; }

    public string SpecialInstructions { get; private set; }

    public LoadInstructionStatus Status { get; private set; }

    public string CancellationReason { get; private set; }

    public IReadOnlyCollection<GeneratedTrip> Trips => _trips.AsReadOnly();

    public decimal CompletedQuantity => _trips
        .Where(trip => trip.Status == GeneratedTripStatus.Completed)
        .Sum(trip => trip.CompletedQuantity ?? 0m);

    public decimal RemainingQuantity => RequestedQuantity - CompletedQuantity;

    public decimal OverDeliveredQuantity => Math.Max(CompletedQuantity - RequestedQuantity, 0m);

    public decimal UnderDeliveredQuantity => Math.Max(RequestedQuantity - CompletedQuantity, 0m);

    public static Result<LoadInstruction> Create(
        Guid id,
        TenantId tenantId,
        LoadInstructionDefinition definition)
    {
        var error = Validate(id, tenantId, definition);
        return error is null
            ? Result<LoadInstruction>.Success(
                new LoadInstruction(id, tenantId, definition))
            : Result<LoadInstruction>.Failure(error);
    }

    public Result Update(LoadInstructionDefinition definition)
    {
        if (Status is LoadInstructionStatus.Completed or LoadInstructionStatus.Cancelled)
        {
            return Result.Failure(new DomainError(
                "instruction.update_status_invalid",
                "Completed or cancelled instructions cannot be changed."));
        }

        var error = Validate(Id, TenantId, definition);
        if (error is not null) return Result.Failure(error);
        if (_trips.Count > 0 && LockedFieldsChanged(definition))
        {
            return Result.Failure(new DomainError(
                "instruction.trip_locked_fields",
                "Contract, route, commodity, quantity, trip count, and windows cannot change after trips are generated."));
        }

        Apply(definition);
        return Result.Success();
    }

    public Result Plan()
    {
        if (Status != LoadInstructionStatus.Draft)
        {
            return Result.Failure(new DomainError(
                "instruction.plan_status_invalid",
                "Only a draft instruction can be planned."));
        }

        Status = LoadInstructionStatus.Planned;
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is LoadInstructionStatus.Completed or LoadInstructionStatus.Cancelled)
        {
            return Result.Failure(new DomainError(
                "instruction.cancel_status_invalid",
                "Completed or cancelled instructions cannot be cancelled."));
        }

        var error = DomainValidation.Required(reason, "instruction.cancellation_reason", 500);
        if (error is not null) return Result.Failure(error);
        CancellationReason = reason.Trim();
        Status = LoadInstructionStatus.Cancelled;
        foreach (var trip in _trips) trip.Cancel();
        return Result.Success();
    }

    public Result GenerateTrips(int count, IReadOnlyList<Guid> tripIds)
    {
        if (Status == LoadInstructionStatus.Cancelled)
        {
            return Result.Failure(new DomainError(
                "instruction.cancelled_trip_generation",
                "A cancelled instruction cannot receive new trips."));
        }

        if (Status != LoadInstructionStatus.Planned)
        {
            return Result.Failure(new DomainError(
                "instruction.trip_generation_status_invalid",
                "Trips can be generated only for a planned instruction."));
        }

        if (count is < 1 or > 1000 || tripIds.Count != count
            || tripIds.Any(id => id == Guid.Empty)
            || tripIds.Distinct().Count() != count)
        {
            return Result.Failure(new DomainError(
                "instruction.trip_count_invalid",
                "Trip count must be between 1 and 1000 with one distinct identifier per trip."));
        }

        var remaining = Math.Max(RemainingQuantity, 0m);
        if (remaining <= 0)
        {
            return Result.Failure(new DomainError(
                "instruction.no_remaining_quantity",
                "The instruction has no remaining quantity to plan."));
        }

        var perTrip = decimal.Round(
            remaining / count,
            3,
            MidpointRounding.AwayFromZero);
        var assigned = 0m;
        for (var index = 0; index < count; index++)
        {
            var quantity = index == count - 1 ? remaining - assigned : perTrip;
            var number = $"{InstructionNumber}-T{_trips.Count + index + 1:000}";
            var trip = GeneratedTrip.Create(
                tripIds[index], TenantId, number, quantity, QuantityUnit);
            if (trip.IsFailure) return Result.Failure(trip.Error!);
            _trips.Add(trip.Value);
            assigned += quantity;
        }

        Status = LoadInstructionStatus.InProgress;
        return Result.Success();
    }

    public Result CompleteTrip(Guid tripId, decimal completedQuantity)
    {
        if (Status == LoadInstructionStatus.Cancelled)
        {
            return Result.Failure(new DomainError(
                "instruction.cancelled_trip_completion",
                "Trips on a cancelled instruction cannot be completed."));
        }

        var trip = _trips.SingleOrDefault(item => item.Id == tripId);
        if (trip is null)
        {
            return Result.Failure(new DomainError(
                "instruction.trip_not_found",
                "The generated trip was not found."));
        }

        var result = trip.Complete(completedQuantity);
        if (result.IsFailure) return result;
        Status = _trips.All(item =>
            item.Status is GeneratedTripStatus.Completed or GeneratedTripStatus.Cancelled)
            ? LoadInstructionStatus.Completed
            : LoadInstructionStatus.InProgress;
        return Result.Success();
    }

    private void Apply(LoadInstructionDefinition definition)
    {
        InstructionNumber = definition.InstructionNumber.Trim().ToUpperInvariant();
        CustomerId = definition.CustomerId;
        TransportContractId = definition.TransportContractId;
        ContractRouteId = definition.ContractRouteId;
        CommodityId = definition.CommodityId;
        RequestedQuantity = decimal.Round(
            definition.RequestedQuantity, 3, MidpointRounding.AwayFromZero);
        QuantityUnit = definition.QuantityUnit;
        RequestedTripCount = definition.RequestedTripCount;
        CollectionWindowStartUtc = definition.CollectionWindowStart.ToUniversalTime();
        CollectionWindowEndUtc = definition.CollectionWindowEnd.ToUniversalTime();
        DeliveryWindowStartUtc = definition.DeliveryWindowStart.ToUniversalTime();
        DeliveryWindowEndUtc = definition.DeliveryWindowEnd.ToUniversalTime();
        CustomerPurchaseOrderReference =
            definition.CustomerPurchaseOrderReference?.Trim() ?? string.Empty;
        CustomerInstructionReference =
            definition.CustomerInstructionReference?.Trim() ?? string.Empty;
        Priority = definition.Priority;
        SpecialInstructions = definition.SpecialInstructions?.Trim() ?? string.Empty;
    }

    private bool LockedFieldsChanged(LoadInstructionDefinition definition) =>
        !string.Equals(
            InstructionNumber,
            definition.InstructionNumber.Trim(),
            StringComparison.OrdinalIgnoreCase)
        || CustomerId != definition.CustomerId
        || TransportContractId != definition.TransportContractId
        || ContractRouteId != definition.ContractRouteId
        || CommodityId != definition.CommodityId
        || RequestedQuantity != decimal.Round(
            definition.RequestedQuantity, 3, MidpointRounding.AwayFromZero)
        || QuantityUnit != definition.QuantityUnit
        || RequestedTripCount != definition.RequestedTripCount
        || CollectionWindowStartUtc != definition.CollectionWindowStart.ToUniversalTime()
        || CollectionWindowEndUtc != definition.CollectionWindowEnd.ToUniversalTime()
        || DeliveryWindowStartUtc != definition.DeliveryWindowStart.ToUniversalTime()
        || DeliveryWindowEndUtc != definition.DeliveryWindowEnd.ToUniversalTime();

    private static DomainError? Validate(
        Guid id,
        TenantId tenantId,
        LoadInstructionDefinition definition)
    {
        if (id == Guid.Empty || tenantId.IsEmpty
            || definition.CustomerId == Guid.Empty
            || definition.TransportContractId == Guid.Empty
            || definition.ContractRouteId == Guid.Empty
            || definition.CommodityId == Guid.Empty)
        {
            return new DomainError(
                "instruction.identifiers_required",
                "Instruction, tenant, customer, contract, route, and commodity identifiers are required.");
        }

        var error = DomainValidation.Required(
            definition.InstructionNumber, "instruction.number", 60);
        if (error is not null) return error;
        if (definition.RequestedQuantity <= 0)
        {
            return new DomainError(
                "instruction.quantity_invalid",
                "Planned quantity must be positive.");
        }

        if (!Enum.IsDefined(definition.QuantityUnit)
            || !Enum.IsDefined(definition.Priority))
        {
            return new DomainError(
                "instruction.enum_invalid",
                "Quantity unit or priority is not supported.");
        }

        if (definition.RequestedTripCount is <= 0 or > 1000)
        {
            return new DomainError(
                "instruction.trip_count_invalid",
                "Requested trip count must be between 1 and 1000 when supplied.");
        }

        if (definition.CollectionWindowEnd < definition.CollectionWindowStart
            || definition.DeliveryWindowEnd < definition.DeliveryWindowStart
            || definition.DeliveryWindowEnd < definition.CollectionWindowStart)
        {
            return new DomainError(
                "instruction.windows_invalid",
                "Collection and delivery windows must be chronological.");
        }

        if (definition.CustomerPurchaseOrderReference?.Trim().Length > 100
            || definition.CustomerInstructionReference?.Trim().Length > 100
            || definition.SpecialInstructions?.Trim().Length > 4000)
        {
            return new DomainError(
                "instruction.text_too_long",
                "References or special instructions exceed their maximum length.");
        }

        return null;
    }
}

public sealed record LoadInstructionDefinition(
    string InstructionNumber,
    Guid CustomerId,
    Guid TransportContractId,
    Guid ContractRouteId,
    Guid CommodityId,
    decimal RequestedQuantity,
    WeightUnit QuantityUnit,
    int? RequestedTripCount,
    DateTimeOffset CollectionWindowStart,
    DateTimeOffset CollectionWindowEnd,
    DateTimeOffset DeliveryWindowStart,
    DateTimeOffset DeliveryWindowEnd,
    string? CustomerPurchaseOrderReference,
    string? CustomerInstructionReference,
    LoadInstructionPriority Priority,
    string? SpecialInstructions);
