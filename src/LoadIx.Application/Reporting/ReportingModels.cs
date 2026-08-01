namespace LoadIx.Application.Reporting;

public sealed record ReportingFilter(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? BranchId = null,
    Guid? CustomerId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ReportingPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DateTimeOffset GeneratedAtUtc,
    string TimeZone);

public sealed record DashboardKpis(
    int ActiveTrips,
    int CompletedTripsToday,
    decimal TonnesDeliveredToday,
    int AwaitingReconciliation,
    int TripsOutsideTolerance,
    int DelayedTrips,
    decimal FleetUtilisationPercent,
    decimal Revenue,
    decimal EstimatedGrossMargin,
    decimal CustomerConcentrationPercent,
    int UpcomingLicenceExpiries,
    DateTimeOffset GeneratedAtUtc,
    string TimeZone,
    string CurrencyCode);

public sealed record TripRegisterRow(
    Guid TripId,
    string TripNumber,
    string Customer,
    string Commodity,
    string Route,
    string Status,
    decimal PlannedQuantity,
    decimal? DeliveredQuantity,
    string? Driver,
    string? Vehicle,
    DateTimeOffset PlannedDeliveryAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsDelayed);

public sealed record TonnageReportRow(
    string Group,
    decimal Tonnes,
    int TripCount,
    decimal Revenue,
    decimal GrossMargin);

public sealed record VarianceReportRow(
    string TripNumber,
    string Customer,
    decimal? SourceNetTonnes,
    decimal? DestinationNetTonnes,
    decimal? VarianceTonnes,
    decimal? VariancePercent,
    bool OutsideTolerance);

public sealed record DriverPerformanceRow(
    string Driver,
    int CompletedTrips,
    decimal DeliveredTonnes,
    int Exceptions,
    decimal OnTimePercent);

public sealed record VehicleUtilisationRow(
    string Vehicle,
    int AssignedTrips,
    int CompletedTrips,
    decimal UtilisationPercent,
    DateOnly? LicenceExpiry,
    DateOnly? RoadworthyExpiry);

public sealed record ContractPerformanceRow(
    string Contract,
    int CompletedTrips,
    decimal DeliveredTonnes,
    decimal Revenue,
    decimal GrossMargin,
    decimal ExceptionPercent);

public sealed record ExceptionReportRow(
    Guid TripId,
    string TripNumber,
    string Customer,
    string Status,
    string Reason,
    DateTimeOffset OccurredAtUtc);

public sealed record PodOutstandingRow(
    Guid TripId,
    string TripNumber,
    string Customer,
    string Status,
    DateTimeOffset CompletedAtUtc,
    int DocumentCount);

public sealed record ExpiryReportRow(
    string ResourceType,
    string Resource,
    DateOnly ExpiryDate,
    int DaysRemaining,
    string Status);

public sealed record InvoiceStatusRow(
    string Status,
    int BatchCount,
    decimal Total,
    DateTimeOffset? LatestCreatedAtUtc);

public sealed record ReportExportJobResponse(
    Guid JobId,
    string Report,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    int Attempts,
    int? RowCount,
    long? ContentLengthBytes,
    string? DownloadUrl,
    string? Error);

public sealed record ReportCsvBatch(byte[] Content, int TotalCount, int RowCount);
