namespace LoadIx.Application.Finance;

public sealed record MoneyAmount(decimal Amount, string CurrencyCode);

public sealed record FinancePagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record FinancialSummary(
    MoneyAmount ExpectedRevenue,
    MoneyAmount TotalCost,
    MoneyAmount GrossMargin,
    decimal MarginPercentage,
    decimal CostPerTonne,
    decimal RevenuePerTonne);

public sealed record CostEntryModel(
    Guid Id,
    string Type,
    decimal Amount,
    string CurrencyCode,
    string Description,
    bool IsAdjustment,
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc);

public sealed record TripFinancialsDetail(
    Guid Id,
    Guid TripId,
    string TripNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    string CurrencyCode,
    string RateType,
    decimal BillableTonnes,
    decimal Subtotal,
    decimal Surcharge,
    decimal TaxExclusiveAmount,
    string TaxTreatment,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal Total,
    DateTimeOffset CalculatedAtUtc,
    string CalculationVersion,
    string ApprovalStatus,
    Guid? InvoiceBatchId,
    FinancialSummary Profitability,
    IReadOnlyList<CostEntryModel> Costs,
    string Version);

public sealed record CostEntryWriteModel(
    string Type,
    decimal Amount,
    string Description,
    bool IsAdjustment = false,
    string? Version = null);

public sealed record ApproveFinancialsModel(string Version, string? Reason = null);

public sealed record TenantTaxConfigurationModel(
    Guid? Id,
    string DefaultTaxTreatment,
    decimal TaxRatePercent,
    bool IsReviewed,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? Version);

public sealed record TenantTaxConfigurationWriteModel(
    string DefaultTaxTreatment,
    decimal TaxRatePercent,
    string? Version = null);

public sealed record InvoiceReadyQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? CustomerId = null,
    Guid? ContractId = null,
    string Sort = "completed",
    string Direction = "asc");

public sealed record InvoiceReadyTrip(
    Guid FinancialsId,
    Guid TripId,
    string TripNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    decimal BillableTonnes,
    decimal TaxExclusiveAmount,
    decimal TaxAmount,
    decimal Total,
    string CurrencyCode,
    DateTimeOffset CompletedAtUtc);

public sealed record InvoiceBatchPreviewRequest(IReadOnlyList<Guid> FinancialsIds);

public sealed record InvoiceBatchPreview(
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    string CurrencyCode,
    decimal TaxExclusiveAmount,
    decimal TaxAmount,
    decimal Total,
    IReadOnlyList<InvoiceBatchLineModel> Lines);

public sealed record InvoiceBatchLineModel(
    Guid Id,
    Guid TripFinancialsId,
    Guid TripId,
    string TripNumber,
    decimal BillableTonnes,
    decimal TaxExclusiveAmount,
    string TaxTreatment,
    decimal TaxAmount,
    decimal Total);

public sealed record InvoiceBatchCreateModel(IReadOnlyList<Guid> FinancialsIds);

public sealed record InvoiceBatchListItem(
    Guid Id,
    string BatchNumber,
    Guid CustomerId,
    string CustomerName,
    string ContractNumber,
    string Status,
    decimal TaxExclusiveAmount,
    decimal TaxAmount,
    decimal Total,
    string CurrencyCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ExportedAtUtc,
    string Version);

public sealed record InvoiceBatchDetail(
    Guid Id,
    string BatchNumber,
    Guid CustomerId,
    string CustomerName,
    Guid TransportContractId,
    string ContractNumber,
    string CurrencyCode,
    string Status,
    decimal TaxExclusiveAmount,
    decimal TaxAmount,
    decimal Total,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    DateTimeOffset? ExportedAtUtc,
    Guid? ExportedByUserId,
    IReadOnlyList<InvoiceBatchLineModel> Lines,
    string Version);

public sealed record VersionedFinanceCommand(
    string Version,
    string? Reason = null);

public enum FinanceFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

#pragma warning disable CA1000
public sealed record FinanceResult<T>(
    T? Value,
    FinanceFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == FinanceFailure.None;

    public static FinanceResult<T> Success(T value) =>
        new(value, FinanceFailure.None, string.Empty, string.Empty);

    public static FinanceResult<T> Fail(
        FinanceFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
#pragma warning restore CA1000
