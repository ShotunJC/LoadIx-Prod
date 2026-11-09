using LoadIx.Domain.Finance;

namespace LoadIx.Application.Finance;

public sealed record PayrollBatchCreateModel(
    string BatchNumber,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    PayrollFrequency Frequency,
    string CurrencyCode);

public sealed record PayrollBatchPreviewRequest(
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    PayrollFrequency Frequency);

public sealed record PayrollBatchPreview(
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    PayrollFrequency Frequency,
    string CurrencyCode,
    int EligibleDriversCount,
    int TotalDeductionsCount,
    decimal TotalDeductionsAmount,
    IReadOnlyList<PayrollBatchPreviewLine> Lines);

public sealed record PayrollBatchPreviewLine(
    Guid DriverId,
    string? DriverEmployeeNumber,
    string DriverName,
    Guid DeductionId,
    string DeductionNumber,
    string TransactionCode,
    string Description,
    decimal InstallmentAmount,
    decimal CurrentBalance,
    decimal BalanceAfterDeduction);

public sealed record PayrollBatchDetail(
    Guid Id,
    Guid TenantId,
    string BatchNumber,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    PayrollFrequency Frequency,
    string CurrencyCode,
    PayrollBatchStatus Status,
    decimal TotalDeductionsAmount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    DateTimeOffset? ExportedAtUtc,
    Guid? ExportedByUserId,
    IReadOnlyList<PayrollBatchLineDetail> Lines);

public sealed record PayrollBatchLineDetail(
    Guid Id,
    Guid DriverId,
    string? DriverEmployeeNumber,
    string DriverName,
    Guid? DeductionId,
    string TransactionCode,
    string Description,
    decimal Amount,
    decimal BalanceAfterDeduction);

public sealed record PayrollBatchListItem(
    Guid Id,
    string BatchNumber,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    PayrollFrequency Frequency,
    string CurrencyCode,
    PayrollBatchStatus Status,
    decimal TotalDeductionsAmount,
    int LineCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExportedAtUtc);
