using LoadIx.Domain.Fleet;

namespace LoadIx.Application.Fleet;

public sealed record DriverWarningCreateModel(
    string WarningNumber,
    DriverWarningType WarningType,
    DriverWarningCategory Category,
    DateTimeOffset IncidentDateUtc,
    DateTimeOffset IssuedDateUtc,
    DateTimeOffset ExpiryDateUtc,
    string Description,
    Guid? TripId = null,
    Guid? DocumentId = null);

public sealed record DriverWarningDetail(
    Guid Id,
    Guid TenantId,
    Guid DriverId,
    string WarningNumber,
    DriverWarningType WarningType,
    DriverWarningCategory Category,
    DriverWarningStatus Status,
    DateTimeOffset IncidentDateUtc,
    DateTimeOffset IssuedDateUtc,
    DateTimeOffset ExpiryDateUtc,
    string Description,
    Guid IssuedByUserId,
    Guid? TripId,
    Guid? DocumentId,
    Guid? RevokedByUserId,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason);

public sealed record RevokeWarningModel(
    string Reason);

public sealed record DriverDeductionCreateModel(
    string DeductionNumber,
    DriverDeductionCategory Category,
    string CurrencyCode,
    decimal TotalAmount,
    decimal InstallmentAmount,
    DateOnly EffectiveDate,
    string Description,
    Guid? WarningId = null,
    Guid? TripId = null,
    Guid? ConsentDocumentId = null);

public sealed record DriverDeductionDetail(
    Guid Id,
    Guid TenantId,
    Guid DriverId,
    string DeductionNumber,
    DriverDeductionCategory Category,
    string CurrencyCode,
    decimal TotalAmount,
    decimal InstallmentAmount,
    decimal RemainingBalance,
    DateOnly EffectiveDate,
    string Description,
    DriverDeductionStatus Status,
    DateTimeOffset CreatedAtUtc,
    Guid? WarningId,
    Guid? TripId,
    Guid? ConsentDocumentId,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAtUtc,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason);

public sealed record CancelDeductionModel(
    string Reason);

public sealed record DriverDisciplinarySummary(
    Guid DriverId,
    string DriverName,
    string? EmployeeNumber,
    int ActiveWarningsCount,
    int TotalWarningsCount,
    decimal TotalActiveDeductionsBalance,
    IReadOnlyList<DriverWarningDetail> ActiveWarnings,
    IReadOnlyList<DriverDeductionDetail> ActiveDeductions);
