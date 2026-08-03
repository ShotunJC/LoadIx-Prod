namespace LoadIx.Application.Trips;

public sealed record TripQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? CustomerId = null,
    Guid? DriverId = null,
    Guid? VehicleId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string Sort = "collection",
    string Direction = "asc");

public sealed record TripPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record TripListItem(
    Guid Id,
    string TripNumber,
    Guid LoadInstructionId,
    string InstructionNumber,
    Guid CustomerId,
    string CustomerName,
    string RouteReference,
    string CommodityName,
    decimal PlannedQuantity,
    string QuantityUnit,
    DateTimeOffset PlannedCollectionAtUtc,
    DateTimeOffset PlannedDeliveryAtUtc,
    string? DriverName,
    string? VehicleReference,
    string Status,
    bool HasException,
    string Version);

public sealed record TripRateSnapshotModel(
    Guid RateId,
    string RateType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? FixedAmount,
    decimal? BaseFee,
    decimal? PerTonneAmount,
    decimal? MinimumCharge,
    decimal? FuelSurchargePercent,
    int RoundingDecimalPlaces,
    string RoundingMode,
    string CurrencyCode);

public sealed record TripTransitionModel(
    Guid Id,
    string PreviousStatus,
    string NewStatus,
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    bool IsManualOverride);

public sealed record TripAssignmentModel(
    Guid Id,
    Guid DriverId,
    string DriverName,
    Guid VehicleId,
    string VehicleReference,
    IReadOnlyList<Guid> TrailerIds,
    IReadOnlyList<string> TrailerReferences,
    Guid ActorUserId,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Reason);

public sealed record TripDetail(
    Guid Id,
    string TripNumber,
    Guid LoadInstructionId,
    string InstructionNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    string ContractVersion,
    Guid ContractRouteId,
    string RouteReference,
    string SourceLocationName,
    string DestinationLocationName,
    Guid CommodityId,
    string CommodityName,
    decimal PlannedQuantity,
    string QuantityUnit,
    TripRateSnapshotModel RateSnapshot,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleReference,
    IReadOnlyList<Guid> TrailerIds,
    IReadOnlyList<string> TrailerReferences,
    DateTimeOffset PlannedCollectionAtUtc,
    DateTimeOffset PlannedDeliveryAtUtc,
    string Status,
    string OperationalNotes,
    string CancellationOrRejectionReason,
    decimal? SourceGrossWeight,
    decimal? SourceTareWeight,
    decimal? SourceNetWeight,
    string? SourceWeighbridgeReference,
    decimal? DestinationGrossWeight,
    decimal? DestinationTareWeight,
    decimal? DestinationNetWeight,
    string? DestinationWeighbridgeReference,
    bool IsReconciled,
    bool HasRequiredDocuments,
    IReadOnlyList<TripTransitionModel> Transitions,
    IReadOnlyList<TripAssignmentModel> Assignments,
    string Version);

public sealed record CreateTripModel(Guid ReservationId);

public sealed record AssignTripModel(
    string Version,
    Guid DriverId,
    Guid VehicleId,
    IReadOnlyList<Guid> TrailerIds,
    string? Reason = null);

public sealed record TripCommandModel(string Version, string? Reason = null);

public sealed record TripOverrideModel(
    string Version,
    string NewStatus,
    string Reason);

public sealed record TripWeighbridgeModel(
    string Version,
    decimal GrossWeight,
    decimal TareWeight,
    string Reference,
    string? Reason = null);

public sealed record TripNoteModel(string Version, string Note);

public sealed record TripLocationUpdateModel(
    string Version,
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMetres,
    DateTimeOffset CapturedAtUtc);

public enum TripFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

#pragma warning disable CA1000 // Matches the established typed-result API.
public sealed record TripResult<T>(
    T? Value,
    TripFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == TripFailure.None;

    public static TripResult<T> Success(T value) =>
        new(value, TripFailure.None, string.Empty, string.Empty);

    public static TripResult<T> Fail(
        TripFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
#pragma warning restore CA1000
