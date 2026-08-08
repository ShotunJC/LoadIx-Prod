using LoadIx.Domain.Commercial;

namespace LoadIx.Application.Contracts;

public sealed record ContractQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    Guid? CustomerId = null,
    string Sort = "contractNumber",
    string Direction = "asc");

public sealed record ContractPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ContractListItem(
    Guid Id,
    string ContractNumber,
    string Name,
    Guid CustomerId,
    string CustomerName,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    string DefaultCurrency,
    int RouteCount,
    int CommodityCount,
    string Version);

public sealed record ContractRouteModel(
    Guid Id,
    string Reference,
    Guid SourceLocationId,
    string SourceLocationName,
    Guid DestinationLocationId,
    string DestinationLocationName,
    bool SourceWeightCaptureRequired,
    bool DestinationWeightCaptureRequired,
    string? CollectionInstructions,
    string? DeliveryInstructions);

public sealed record ContractRateModel(
    Guid Id,
    Guid ContractRouteId,
    string RateType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? FixedAmount,
    decimal? BaseFee,
    decimal? PerTonneAmount,
    decimal? MinimumCharge,
    decimal? FuelSurchargePercent,
    int RoundingDecimalPlaces,
    string RoundingMode);

public sealed record ContractCommodityModel(
    Guid CommodityId,
    string CommodityCode,
    string CommodityName);

public sealed record ContractDetail(
    Guid Id,
    string ContractNumber,
    string Name,
    Guid CustomerId,
    string CustomerName,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    string DefaultCurrency,
    int PaymentTermsDays,
    decimal WeightTolerancePercent,
    bool ManualQuantityPermitted,
    string? Notes,
    IReadOnlyList<ContractRouteModel> Routes,
    IReadOnlyList<ContractRateModel> Rates,
    IReadOnlyList<ContractCommodityModel> Commodities,
    string Version);

public sealed record ContractRouteWriteModel(
    Guid? Id,
    string Reference,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    bool SourceWeightCaptureRequired = true,
    bool DestinationWeightCaptureRequired = true,
    string? CollectionInstructions = null,
    string? DeliveryInstructions = null);

public sealed record ContractRateWriteModel(
    Guid? Id,
    Guid ContractRouteId,
    string RateType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? FixedAmount,
    decimal? BaseFee,
    decimal? PerTonneAmount,
    decimal? MinimumCharge,
    decimal? FuelSurchargePercent,
    int RoundingDecimalPlaces = 2,
    string RoundingMode = "AwayFromZero");

public sealed record ContractWriteModel(
    Guid CustomerId,
    string ContractNumber,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    string DefaultCurrency,
    int PaymentTermsDays,
    decimal WeightTolerancePercent,
    string? Notes,
    IReadOnlyList<ContractRouteWriteModel> Routes,
    IReadOnlyList<Guid> CommodityIds,
    IReadOnlyList<ContractRateWriteModel> Rates,
    string? Version = null,
    bool ManualQuantityPermitted = false);

public sealed record ContractTransitionModel(string Version);

public sealed record RatePreviewModel(
    Guid RateId,
    decimal? SourceNetTonnes,
    decimal? DestinationNetTonnes,
    decimal? ReconciledTonnes,
    DateOnly CalculationDate);

public sealed record RateCalculationResponse(
    decimal TotalAmount,
    string CurrencyCode,
    decimal BillableTonnes,
    IReadOnlyList<RateCalculationLine> Breakdown,
    ContractRateSnapshot RateSnapshot,
    RateCalculationInput InputSnapshot);

public sealed record ContractHistoryItem(
    long Id,
    string EventType,
    string Outcome,
    Guid? ActorUserId,
    DateTimeOffset OccurredAtUtc,
    bool IsSupportContext);

public enum ContractFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record ContractResult<T>(
    T? Value,
    ContractFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == ContractFailure.None;

    public static ContractResult<T> Success(T value) =>
        new(value, ContractFailure.None, string.Empty, string.Empty);

    public static ContractResult<T> Fail(
        ContractFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
