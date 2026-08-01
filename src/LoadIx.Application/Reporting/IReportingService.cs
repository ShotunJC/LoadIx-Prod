namespace LoadIx.Application.Reporting;

public interface IReportingService
{
    Task<DashboardKpis> GetDashboardKpisAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TripRegisterRow>> GetTripRegisterAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCustomerAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCommodityAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByRouteAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<VarianceReportRow>> GetVarianceAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<DriverPerformanceRow>> GetDriverPerformanceAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<VehicleUtilisationRow>> GetVehicleUtilisationAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<ContractPerformanceRow>> GetContractPerformanceAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TonnageReportRow>> GetCustomerRevenueAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<TonnageReportRow>> GetTripProfitabilityAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<ExceptionReportRow>> GetExceptionsAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<PodOutstandingRow>> GetOutstandingPodAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<ExpiryReportRow>> GetExpiriesAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportingPagedResponse<InvoiceStatusRow>> GetInvoiceStatusAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<byte[]> ExportCsvAsync(
        string report,
        ReportingFilter filter,
        CancellationToken cancellationToken);

    Task<ReportCsvBatch> ExportCsvBatchAsync(
        string report,
        ReportingFilter filter,
        bool includeHeader,
        CancellationToken cancellationToken);
}
