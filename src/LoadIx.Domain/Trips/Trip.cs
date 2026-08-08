using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;

namespace LoadIx.Domain.Trips;

public sealed class Trip : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<TripTransition> _transitions = [];
    private readonly List<TripAssignment> _assignments = [];

    private Trip(
        Guid id,
        TenantId tenantId,
        TripDefinition definition,
        TripRateSnapshot rateSnapshot)
        : base(id)
    {
        TripNumber = string.Empty;
        ContractNumber = string.Empty;
        RouteReference = string.Empty;
        OperationalNotes = string.Empty;
        CancellationOrRejectionReason = string.Empty;
        TenantId = tenantId;
        ApplyDefinition(definition);
        RateSnapshot = rateSnapshot;
        QuantitySource = rateSnapshot.RateType == ContractRateType.FixedPerTrip
            ? TripQuantitySource.Fixed
            : TripQuantitySource.Pending;
        Status = TripStatus.Planned;
    }

    private Trip()
    {
        TripNumber = string.Empty;
        ContractNumber = string.Empty;
        RouteReference = string.Empty;
        OperationalNotes = string.Empty;
        CancellationOrRejectionReason = string.Empty;
        RateSnapshot = null!;
    }

    public TenantId TenantId { get; private set; }

    public string TripNumber { get; private set; }

    public Guid LoadInstructionId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid TransportContractId { get; private set; }

    public string ContractNumber { get; private set; }

    public string ContractVersion { get; private set; } = string.Empty;

    public Guid ContractRouteId { get; private set; }

    public string RouteReference { get; private set; }

    public Guid CommodityId { get; private set; }

    public decimal PlannedQuantity { get; private set; }

    public WeightUnit QuantityUnit { get; private set; }

    public TripRateSnapshot RateSnapshot { get; private set; }

    public Guid? DriverId { get; private set; }

    public Guid? VehicleId { get; private set; }

    public string TrailerIds { get; private set; } = string.Empty;

    public DateTimeOffset PlannedCollectionAtUtc { get; private set; }

    public DateTimeOffset PlannedDeliveryAtUtc { get; private set; }

    public TripStatus Status { get; private set; }

    public string OperationalNotes { get; private set; }

    public string CancellationOrRejectionReason { get; private set; }

    public DateTimeOffset? AssignedAtUtc { get; private set; }

    public DateTimeOffset? DispatchedAtUtc { get; private set; }

    public DateTimeOffset? CollectionArrivalAtUtc { get; private set; }

    public DateTimeOffset? LoadingStartedAtUtc { get; private set; }

    public DateTimeOffset? CollectionCompletedAtUtc { get; private set; }

    public DateTimeOffset? InTransitAtUtc { get; private set; }

    public DateTimeOffset? DeliveryArrivalAtUtc { get; private set; }

    public DateTimeOffset? UnloadingStartedAtUtc { get; private set; }

    public DateTimeOffset? DeliveryCompletedAtUtc { get; private set; }

    public DateTimeOffset? ReconciliationSubmittedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public DateTimeOffset? RejectedAtUtc { get; private set; }

    public decimal? SourceGrossWeight { get; private set; }

    public decimal? SourceTareWeight { get; private set; }

    public string? SourceWeighbridgeReference { get; private set; }

    public decimal? DestinationGrossWeight { get; private set; }

    public decimal? DestinationTareWeight { get; private set; }

    public string? DestinationWeighbridgeReference { get; private set; }

    public bool IsReconciled { get; private set; }

    public bool HasRequiredDocuments { get; private set; }

    public bool SourceWeightCaptureRequired { get; private set; }

    public bool DestinationWeightCaptureRequired { get; private set; }

    public bool ManualQuantityPermitted { get; private set; }

    public decimal WeightTolerancePercent { get; private set; }

    public decimal? ReconciledQuantity { get; private set; }

    public decimal? ManualQuantity { get; private set; }

    public TripReconciliationStatus ReconciliationStatus { get; private set; }

    public TripQuantitySource QuantitySource { get; private set; }

    public IReadOnlyCollection<TripTransition> Transitions =>
        _transitions.AsReadOnly();

    public IReadOnlyCollection<TripAssignment> Assignments =>
        _assignments.AsReadOnly();

    public IReadOnlyList<Guid> CurrentTrailerIds =>
        string.IsNullOrWhiteSpace(TrailerIds)
            ? []
            : TrailerIds.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToArray();

    public decimal? SourceNetWeight =>
        SourceGrossWeight - SourceTareWeight;

    public decimal? DestinationNetWeight =>
        DestinationGrossWeight - DestinationTareWeight;

    public static Result<Trip> Create(
        Guid id,
        TenantId tenantId,
        TripDefinition definition,
        TripRateSnapshot rateSnapshot)
    {
        var error = Validate(id, tenantId, definition, rateSnapshot);
        return error is null
            ? Result<Trip>.Success(new Trip(id, tenantId, definition, rateSnapshot))
            : Result<Trip>.Failure(error);
    }

    public Result Assign(
        Guid driverId,
        Guid vehicleId,
        IReadOnlyCollection<Guid> trailerIds,
        TripCommandContext context)
    {
        if (Status != TripStatus.Planned)
        {
            return InvalidTransition(TripStatus.Assigned);
        }

        return ApplyAssignment(
            driverId, vehicleId, trailerIds, context, false);
    }

    public Result Reassign(
        Guid driverId,
        Guid vehicleId,
        IReadOnlyCollection<Guid> trailerIds,
        TripCommandContext context)
    {
        if (Status == TripStatus.Assigned)
        {
            return ApplyAssignment(
                driverId, vehicleId, trailerIds, context, true);
        }

        if (!context.IsManualOverride
            || Status is TripStatus.Completed or TripStatus.Cancelled
                or TripStatus.Rejected)
        {
            return Result.Failure(new DomainError(
                "trip.reassignment_status_invalid",
                "Post-dispatch reassignment requires a manual override."));
        }

        return ApplyAssignment(
            driverId, vehicleId, trailerIds, context, true);
    }

    public Result Dispatch(TripCommandContext context)
    {
        if (DriverId is null || VehicleId is null)
        {
            return Result.Failure(new DomainError(
                "trip.assignments_required",
                "A driver and vehicle must be assigned before dispatch."));
        }

        return Transition(TripStatus.Assigned, TripStatus.Dispatched, context);
    }

    public Result ConfirmCollectionArrival(TripCommandContext context) =>
        Transition(TripStatus.Dispatched, TripStatus.AtCollection, context);

    public Result StartLoading(TripCommandContext context) =>
        Transition(TripStatus.AtCollection, TripStatus.Loading, context);

    public Result CompleteCollection(TripCommandContext context)
    {
        if (SourceWeightCaptureRequired
            && !HasSourceWeighbridgeInformation
            && !context.IsManualOverride)
        {
            return MissingEvidence("source weighbridge");
        }

        return Transition(TripStatus.Loading, TripStatus.CollectionComplete, context);
    }

    public Result DepartCollection(TripCommandContext context) =>
        Transition(TripStatus.CollectionComplete, TripStatus.InTransit, context);

    public Result ConfirmDeliveryArrival(TripCommandContext context) =>
        Transition(TripStatus.InTransit, TripStatus.AtDelivery, context);

    public Result StartUnloading(TripCommandContext context) =>
        Transition(TripStatus.AtDelivery, TripStatus.Unloading, context);

    public Result CompleteDelivery(TripCommandContext context)
    {
        if (DestinationWeightCaptureRequired
            && !HasDestinationWeighbridgeInformation
            && !context.IsManualOverride)
        {
            return MissingEvidence("destination weighbridge");
        }

        return Transition(TripStatus.Unloading, TripStatus.DeliveryComplete, context);
    }

    public Result SubmitForReconciliation(TripCommandContext context)
    {
        if ((SourceWeightCaptureRequired && !HasSourceWeighbridgeInformation
                || DestinationWeightCaptureRequired
                    && !HasDestinationWeighbridgeInformation)
            && !context.IsManualOverride)
        {
            return MissingEvidence("source and destination weighbridge");
        }

        if (!SourceWeightCaptureRequired && !DestinationWeightCaptureRequired
            && RateSnapshot.RateType == ContractRateType.ManualQuantityPerTonne
            && !ManualQuantity.HasValue)
        {
            return MissingEvidence("an authorised manual quantity");
        }

        var transition = Transition(
            TripStatus.DeliveryComplete,
            TripStatus.ReconciliationRequired,
            context);
        if (transition.IsFailure) return transition;

        if (!SourceWeightCaptureRequired && !DestinationWeightCaptureRequired)
        {
            ReconciliationStatus = ManualQuantity.HasValue
                ? TripReconciliationStatus.ManualQuantity
                : TripReconciliationStatus.NotApplicable;
            QuantitySource = ManualQuantity.HasValue
                ? TripQuantitySource.Manual
                : TripQuantitySource.NonWeighed;
            ReconciledQuantity = ManualQuantity;
            IsReconciled = true;
            return Result.Success();
        }

        ReconciledQuantity = DestinationNetWeight ?? SourceNetWeight;
        QuantitySource = SourceWeightCaptureRequired
            && DestinationWeightCaptureRequired
                ? TripQuantitySource.ReconciledMeasured
                : DestinationWeightCaptureRequired
                    ? TripQuantitySource.DestinationMeasured
                    : TripQuantitySource.SourceMeasured;
        if (SourceWeightCaptureRequired && DestinationWeightCaptureRequired)
        {
            var baseline = Math.Max(SourceNetWeight!.Value, DestinationNetWeight!.Value);
            var variance = baseline == 0m
                ? 0m
                : Math.Abs(SourceNetWeight.Value - DestinationNetWeight.Value)
                    / baseline * 100m;
            ReconciliationStatus = variance <= WeightTolerancePercent
                ? TripReconciliationStatus.WithinTolerance
                : TripReconciliationStatus.ExceptionReviewRequired;
            IsReconciled = variance <= WeightTolerancePercent;
        }
        else
        {
            ReconciliationStatus = TripReconciliationStatus.SingleMeasurement;
            IsReconciled = true;
        }

        return Result.Success();
    }

    public Result Complete(TripCommandContext context)
    {
        if ((!IsReconciled || !HasRequiredDocuments)
            && !context.IsManualOverride)
        {
            return MissingEvidence("reconciliation and required documents");
        }

        return Transition(
            TripStatus.ReconciliationRequired,
            TripStatus.Completed,
            context);
    }

    public Result Cancel(TripCommandContext context)
    {
        if (Status is TripStatus.Completed or TripStatus.Cancelled
            or TripStatus.Rejected)
        {
            return InvalidTransition(TripStatus.Cancelled);
        }

        return TransitionFromCurrent(TripStatus.Cancelled, context, true);
    }

    public Result Reject(TripCommandContext context)
    {
        if (Status is TripStatus.Completed or TripStatus.Cancelled
            or TripStatus.Rejected)
        {
            return InvalidTransition(TripStatus.Rejected);
        }

        return TransitionFromCurrent(TripStatus.Rejected, context, true);
    }

    public Result RecordSourceWeighbridge(
        decimal grossWeight,
        decimal tareWeight,
        string reference)
    {
        if (IsTerminal)
        {
            return Result.Failure(new DomainError(
                "trip.weighbridge_terminal",
                "Weighbridge information cannot be changed after a trip is finalised."));
        }

        var error = ValidateWeighbridge(grossWeight, tareWeight, reference);
        if (error is not null) return Result.Failure(error);
        SourceGrossWeight = RoundWeight(grossWeight);
        SourceTareWeight = RoundWeight(tareWeight);
        SourceWeighbridgeReference = reference.Trim();
        return Result.Success();
    }

    public Result RecordDestinationWeighbridge(
        decimal grossWeight,
        decimal tareWeight,
        string reference)
    {
        if (IsTerminal)
        {
            return Result.Failure(new DomainError(
                "trip.weighbridge_terminal",
                "Weighbridge information cannot be changed after a trip is finalised."));
        }

        var error = ValidateWeighbridge(grossWeight, tareWeight, reference);
        if (error is not null) return Result.Failure(error);
        DestinationGrossWeight = RoundWeight(grossWeight);
        DestinationTareWeight = RoundWeight(tareWeight);
        DestinationWeighbridgeReference = reference.Trim();
        return Result.Success();
    }

    public Result ConfirmReconciliation()
    {
        if (Status != TripStatus.ReconciliationRequired)
        {
            return Result.Failure(new DomainError(
                "trip.reconciliation_status_invalid",
                "Reconciliation can be confirmed only when it is required."));
        }

        IsReconciled = true;
        if (ReconciliationStatus == TripReconciliationStatus.ExceptionReviewRequired)
        {
            ReconciliationStatus = TripReconciliationStatus.ApprovedException;
        }
        return Result.Success();
    }

    public Result RecordManualQuantity(
        decimal quantity,
        TripCommandContext context)
    {
        if (!ManualQuantityPermitted || !context.IsManualOverride)
        {
            return Result.Failure(new DomainError(
                "trip.manual_quantity_permission_required",
                "Manual quantity capture requires contract approval and explicit override authority."));
        }

        if (IsTerminal || quantity <= 0m)
        {
            return Result.Failure(new DomainError(
                "trip.manual_quantity_invalid",
                "Manual quantity must be positive and cannot change a finalised trip."));
        }

        ManualQuantity = RoundWeight(quantity);
        QuantitySource = TripQuantitySource.Manual;
        ReconciliationStatus = TripReconciliationStatus.ManualQuantity;
        return Result.Success();
    }

    public void ConfirmRequiredDocuments() => HasRequiredDocuments = true;

    public Result<decimal> ResolveBillableTonnes()
    {
        decimal? measuredQuantity = RateSnapshot.RateType switch
        {
            ContractRateType.FixedPerTrip => 0m,
            ContractRateType.SourceNetTonne => SourceNetWeight,
            ContractRateType.DestinationNetTonne => DestinationNetWeight,
            ContractRateType.ReconciledTonne or ContractRateType.BasePlusPerTonne =>
                ReconciledQuantity,
            ContractRateType.ManualQuantityPerTonne => ManualQuantity,
            _ => null,
        };
        if (!measuredQuantity.HasValue)
        {
            return Result<decimal>.Failure(new DomainError(
                "finance.billable_weight_missing",
                "The weight required by the trip rate is unavailable."));
        }

        var tonnes = QuantityUnit == WeightUnit.Kilograms
            ? measuredQuantity.Value / 1000m
            : measuredQuantity.Value;
        return Result<decimal>.Success(decimal.Round(
            tonnes,
            3,
            MidpointRounding.AwayFromZero));
    }

    public Result AddOperationalNote(string note)
    {
        var error = DomainValidation.Required(note, "trip.note_required", 4000);
        if (error is not null) return Result.Failure(error);
        OperationalNotes = string.IsNullOrWhiteSpace(OperationalNotes)
            ? note.Trim()
            : $"{OperationalNotes}{Environment.NewLine}{note.Trim()}";
        if (OperationalNotes.Length > 4000)
        {
            return Result.Failure(new DomainError(
                "trip.notes_too_long",
                "Operational notes cannot exceed 4000 characters."));
        }

        return Result.Success();
    }

    public Result ManualOverride(
        TripStatus newStatus,
        TripCommandContext context)
    {
        if (!context.IsManualOverride)
        {
            return Result.Failure(new DomainError(
                "trip.override_permission_required",
                "A manual override requires explicit override authority."));
        }

        if (!Enum.IsDefined(newStatus)
            || Status is TripStatus.Completed or TripStatus.Cancelled
                or TripStatus.Rejected)
        {
            return InvalidTransition(newStatus);
        }

        return TransitionFromCurrent(newStatus, context, true);
    }

    private bool HasSourceWeighbridgeInformation =>
        SourceGrossWeight.HasValue
        && SourceTareWeight.HasValue
        && !string.IsNullOrWhiteSpace(SourceWeighbridgeReference);

    private bool HasDestinationWeighbridgeInformation =>
        DestinationGrossWeight.HasValue
        && DestinationTareWeight.HasValue
        && !string.IsNullOrWhiteSpace(DestinationWeighbridgeReference);

    private bool IsTerminal => Status is TripStatus.Completed
        or TripStatus.Cancelled
        or TripStatus.Rejected;

    private Result ApplyAssignment(
        Guid driverId,
        Guid vehicleId,
        IReadOnlyCollection<Guid> trailerIds,
        TripCommandContext context,
        bool isReassignment)
    {
        if (driverId == Guid.Empty || vehicleId == Guid.Empty
            || trailerIds.Any(id => id == Guid.Empty)
            || trailerIds.Distinct().Count() != trailerIds.Count
            || trailerIds.Count > 2)
        {
            return Result.Failure(new DomainError(
                "trip.assignment_invalid",
                "Assignments require a driver, vehicle, and up to two distinct trailers."));
        }

        _assignments.LastOrDefault(item => item.EndedAtUtc is null)
            ?.End(context.OccurredAtUtc);
        DriverId = driverId;
        VehicleId = vehicleId;
        TrailerIds = string.Join(',', trailerIds.Order());
        _assignments.Add(TripAssignment.Create(
            TenantId,
            driverId,
            vehicleId,
            trailerIds,
            context.ActorUserId,
            context.OccurredAtUtc,
            context.Reason));
        AssignedAtUtc = context.OccurredAtUtc;
        return isReassignment
            ? Result.Success()
            : Transition(TripStatus.Planned, TripStatus.Assigned, context);
    }

    private Result Transition(
        TripStatus expectedStatus,
        TripStatus newStatus,
        TripCommandContext context) =>
        Status != expectedStatus && !context.IsManualOverride
            ? InvalidTransition(newStatus)
            : TransitionFromCurrent(newStatus, context, false);

    private Result TransitionFromCurrent(
        TripStatus newStatus,
        TripCommandContext context,
        bool reasonRequired)
    {
        if (context.ActorUserId == Guid.Empty
            || context.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "trip.transition_context_invalid",
                "Trip transitions require an actor and UTC timestamp."));
        }

        if ((reasonRequired || context.IsManualOverride)
            && string.IsNullOrWhiteSpace(context.Reason))
        {
            return Result.Failure(new DomainError(
                "trip.transition_reason_required",
                "A reason is required for cancellation, rejection, or override."));
        }

        if (context.Reason?.Trim().Length > 500)
        {
            return Result.Failure(new DomainError(
                "trip.transition_reason_too_long",
                "Transition reasons cannot exceed 500 characters."));
        }

        var previous = Status;
        Status = newStatus;
        SetTimestamp(newStatus, context.OccurredAtUtc);
        if (newStatus is TripStatus.Cancelled or TripStatus.Rejected)
        {
            CancellationOrRejectionReason = context.Reason!.Trim();
        }

        _transitions.Add(TripTransition.Create(
            TenantId,
            previous,
            newStatus,
            context.ActorUserId,
            context.OccurredAtUtc,
            context.Reason,
            context.IsManualOverride));
        RaiseDomainEvent(new TripStatusChanged(
            Id,
            TenantId,
            previous,
            newStatus,
            context.ActorUserId,
            context.IsManualOverride,
            context.OccurredAtUtc));
        return Result.Success();
    }

    private void SetTimestamp(TripStatus status, DateTimeOffset timestamp)
    {
        switch (status)
        {
            case TripStatus.Assigned: AssignedAtUtc = timestamp; break;
            case TripStatus.Dispatched: DispatchedAtUtc = timestamp; break;
            case TripStatus.AtCollection: CollectionArrivalAtUtc = timestamp; break;
            case TripStatus.Loading: LoadingStartedAtUtc = timestamp; break;
            case TripStatus.CollectionComplete: CollectionCompletedAtUtc = timestamp; break;
            case TripStatus.InTransit: InTransitAtUtc = timestamp; break;
            case TripStatus.AtDelivery: DeliveryArrivalAtUtc = timestamp; break;
            case TripStatus.Unloading: UnloadingStartedAtUtc = timestamp; break;
            case TripStatus.DeliveryComplete: DeliveryCompletedAtUtc = timestamp; break;
            case TripStatus.ReconciliationRequired:
                ReconciliationSubmittedAtUtc = timestamp;
                break;
            case TripStatus.Completed: CompletedAtUtc = timestamp; break;
            case TripStatus.Cancelled: CancelledAtUtc = timestamp; break;
            case TripStatus.Rejected: RejectedAtUtc = timestamp; break;
            case TripStatus.Planned:
            default:
                break;
        }
    }

    private void ApplyDefinition(TripDefinition definition)
    {
        TripNumber = definition.TripNumber.Trim().ToUpperInvariant();
        LoadInstructionId = definition.LoadInstructionId;
        CustomerId = definition.CustomerId;
        TransportContractId = definition.TransportContractId;
        ContractNumber = definition.ContractNumber.Trim().ToUpperInvariant();
        ContractVersion = definition.ContractVersion;
        ContractRouteId = definition.ContractRouteId;
        RouteReference = definition.RouteReference.Trim().ToUpperInvariant();
        CommodityId = definition.CommodityId;
        PlannedQuantity = RoundWeight(definition.PlannedQuantity);
        QuantityUnit = definition.QuantityUnit;
        PlannedCollectionAtUtc =
            definition.PlannedCollectionAtUtc.ToUniversalTime();
        PlannedDeliveryAtUtc =
            definition.PlannedDeliveryAtUtc.ToUniversalTime();
        SourceWeightCaptureRequired = definition.SourceWeightCaptureRequired;
        DestinationWeightCaptureRequired = definition.DestinationWeightCaptureRequired;
        ManualQuantityPermitted = definition.ManualQuantityPermitted;
        WeightTolerancePercent = definition.WeightTolerancePercent;
        ReconciliationStatus = TripReconciliationStatus.Pending;
        QuantitySource = TripQuantitySource.Pending;
    }

    private static DomainError? Validate(
        Guid id,
        TenantId tenantId,
        TripDefinition definition,
        TripRateSnapshot rateSnapshot)
    {
        if (id == Guid.Empty || tenantId.IsEmpty
            || definition.LoadInstructionId == Guid.Empty
            || definition.CustomerId == Guid.Empty
            || definition.TransportContractId == Guid.Empty
            || definition.ContractRouteId == Guid.Empty
            || definition.CommodityId == Guid.Empty
            || rateSnapshot.RateId == Guid.Empty)
        {
            return new DomainError(
                "trip.identifiers_required",
                "Trip, tenant, instruction, commercial, and rate identifiers are required.");
        }

        var error = DomainValidation.Required(
                definition.TripNumber, "trip.number", 80)
            ?? DomainValidation.Required(
                definition.ContractNumber, "trip.contract_number", 80)
            ?? DomainValidation.Required(
                definition.RouteReference, "trip.route_reference", 80);
        if (error is not null) return error;
        if (definition.PlannedQuantity <= 0
            || !Enum.IsDefined(definition.QuantityUnit))
        {
            return new DomainError(
                "trip.planned_quantity_invalid",
                "A trip requires a positive planned quantity and supported unit.");
        }

        if (definition.PlannedDeliveryAtUtc
            < definition.PlannedCollectionAtUtc)
        {
            return new DomainError(
                "trip.planned_dates_invalid",
                "Planned delivery cannot precede collection.");
        }

        if (definition.WeightTolerancePercent is < 0m or > 100m)
        {
            return new DomainError(
                "trip.tolerance_invalid",
                "Trip weight tolerance must be between 0 and 100 percent.");
        }

        return null;
    }

    private static DomainError? ValidateWeighbridge(
        decimal grossWeight,
        decimal tareWeight,
        string reference)
    {
        if (grossWeight <= 0 || tareWeight < 0 || grossWeight <= tareWeight)
        {
            return new DomainError(
                "trip.weighbridge_weights_invalid",
                "Gross weight must be positive and greater than tare weight.");
        }

        return DomainValidation.Required(
            reference, "trip.weighbridge_reference", 120);
    }

    private static decimal RoundWeight(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    private Result InvalidTransition(TripStatus target) =>
        Result.Failure(new DomainError(
            "trip.transition_invalid",
            $"A trip cannot transition from {Status} to {target}."));

    private static Result MissingEvidence(string evidence) =>
        Result.Failure(new DomainError(
            "trip.required_evidence_missing",
            $"The trip requires {evidence} information before this transition."));
}

public sealed record TripDefinition(
    string TripNumber,
    Guid LoadInstructionId,
    Guid CustomerId,
    Guid TransportContractId,
    string ContractNumber,
    string ContractVersion,
    Guid ContractRouteId,
    string RouteReference,
    Guid CommodityId,
    decimal PlannedQuantity,
    WeightUnit QuantityUnit,
    DateTimeOffset PlannedCollectionAtUtc,
    DateTimeOffset PlannedDeliveryAtUtc,
    bool SourceWeightCaptureRequired = true,
    bool DestinationWeightCaptureRequired = true,
    bool ManualQuantityPermitted = false,
    decimal WeightTolerancePercent = 0m);

public enum TripReconciliationStatus
{
    Pending,
    NotApplicable,
    SingleMeasurement,
    WithinTolerance,
    ExceptionReviewRequired,
    ApprovedException,
    ManualQuantity,
}

public enum TripQuantitySource
{
    Pending,
    Fixed,
    SourceMeasured,
    DestinationMeasured,
    ReconciledMeasured,
    Manual,
    NonWeighed,
}

public sealed record TripCommandContext(
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc,
    string? Reason = null,
    bool IsManualOverride = false);
