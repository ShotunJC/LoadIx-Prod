namespace LoadIx.Application.Instructions;

public sealed record InstructionQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? CustomerId = null,
    Guid? ContractId = null,
    string? Priority = null,
    string Sort = "collectionWindow",
    string Direction = "asc");

public sealed record InstructionPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record InstructionListItem(
    Guid Id,
    string InstructionNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    string CommodityName,
    decimal RequestedQuantity,
    decimal CompletedQuantity,
    decimal RemainingQuantity,
    string QuantityUnit,
    DateTimeOffset CollectionWindowStartUtc,
    string Priority,
    string Status,
    int GeneratedTripCount,
    string Version);

public sealed record GeneratedTripModel(
    Guid Id,
    string TripNumber,
    decimal PlannedQuantity,
    decimal? CompletedQuantity,
    string QuantityUnit,
    string Status);

public sealed record InstructionDetail(
    Guid Id,
    string InstructionNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    Guid ContractRouteId,
    string RouteReference,
    string SourceLocationName,
    string DestinationLocationName,
    Guid CommodityId,
    string CommodityCode,
    string CommodityName,
    decimal RequestedQuantity,
    decimal CompletedQuantity,
    decimal RemainingQuantity,
    decimal OverDeliveredQuantity,
    decimal UnderDeliveredQuantity,
    string QuantityUnit,
    int? RequestedTripCount,
    DateTimeOffset CollectionWindowStartUtc,
    DateTimeOffset CollectionWindowEndUtc,
    DateTimeOffset DeliveryWindowStartUtc,
    DateTimeOffset DeliveryWindowEndUtc,
    string CustomerPurchaseOrderReference,
    string CustomerInstructionReference,
    string Priority,
    string SpecialInstructions,
    string Status,
    string CancellationReason,
    Guid? CreatedByUserId,
    IReadOnlyList<GeneratedTripModel> Trips,
    string Version);

public sealed record InstructionWriteModel(
    string InstructionNumber,
    Guid CustomerId,
    Guid TransportContractId,
    Guid ContractRouteId,
    Guid CommodityId,
    decimal RequestedQuantity,
    string QuantityUnit,
    int? RequestedTripCount,
    DateTimeOffset CollectionWindowStart,
    DateTimeOffset CollectionWindowEnd,
    DateTimeOffset DeliveryWindowStart,
    DateTimeOffset DeliveryWindowEnd,
    string? CustomerPurchaseOrderReference,
    string? CustomerInstructionReference,
    string Priority,
    string? SpecialInstructions,
    string? Version = null);

public sealed record InstructionTransitionModel(string Version);

public sealed record InstructionCancellationModel(string Version, string Reason);

public sealed record GenerateTripsModel(string Version, int Count);

public sealed record InstructionHistoryItem(
    long Id,
    string EventType,
    string Outcome,
    Guid? ActorUserId,
    DateTimeOffset OccurredAtUtc,
    bool IsSupportContext);

public enum InstructionFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record InstructionResult<T>(
    T? Value,
    InstructionFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == InstructionFailure.None;

    public static InstructionResult<T> Success(T value) =>
        new(value, InstructionFailure.None, string.Empty, string.Empty);

    public static InstructionResult<T> Fail(
        InstructionFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
