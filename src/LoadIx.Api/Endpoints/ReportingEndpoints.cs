using LoadIx.Application.Abstractions;
using LoadIx.Application.Reporting;
using LoadIx.Application.Security;

namespace LoadIx.Api.Endpoints;

public static class ReportingEndpoints
{
    private static readonly HashSet<string> ReportNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "trip-register", "tonnage-by-customer", "tonnage-by-commodity", "tonnage-by-route",
        "variance", "driver-performance", "vehicle-utilisation", "contract-performance",
        "customer-revenue", "trip-profitability", "exceptions", "outstanding-pod", "expiries", "invoice-status",
    };

    public static RouteGroupBuilder MapReportingEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/reports").WithTags("Reporting")
            .RequireAuthorization(LoadIxPermissions.ReportView);
        group.MapGet("/dashboard", async (
            [AsParameters] ReportingQueryParameters query,
            IReportingService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDashboardKpisAsync(query.ToFilter(), cancellationToken)));
        group.MapGet("/{report}", async (
            string report,
            [AsParameters] ReportingQueryParameters query,
            IReportingService service,
            CancellationToken cancellationToken) =>
        {
            if (!ReportNames.Contains(report))
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "Report not found", detail: "The requested report is not available.",
                    extensions: new Dictionary<string, object?> { ["code"] = "report.not_found" });
            }

            var filter = query.ToFilter();
            return report.ToLowerInvariant() switch
            {
                "trip-register" => Results.Ok(await service.GetTripRegisterAsync(filter, cancellationToken)),
                "tonnage-by-customer" => Results.Ok(await service.GetTonnageByCustomerAsync(filter, cancellationToken)),
                "tonnage-by-commodity" => Results.Ok(await service.GetTonnageByCommodityAsync(filter, cancellationToken)),
                "tonnage-by-route" => Results.Ok(await service.GetTonnageByRouteAsync(filter, cancellationToken)),
                "variance" => Results.Ok(await service.GetVarianceAsync(filter, cancellationToken)),
                "driver-performance" => Results.Ok(await service.GetDriverPerformanceAsync(filter, cancellationToken)),
                "vehicle-utilisation" => Results.Ok(await service.GetVehicleUtilisationAsync(filter, cancellationToken)),
                "contract-performance" => Results.Ok(await service.GetContractPerformanceAsync(filter, cancellationToken)),
                "customer-revenue" => Results.Ok(await service.GetCustomerRevenueAsync(filter, cancellationToken)),
                "trip-profitability" => Results.Ok(await service.GetTripProfitabilityAsync(filter, cancellationToken)),
                "exceptions" => Results.Ok(await service.GetExceptionsAsync(filter, cancellationToken)),
                "outstanding-pod" => Results.Ok(await service.GetOutstandingPodAsync(filter, cancellationToken)),
                "expiries" => Results.Ok(await service.GetExpiriesAsync(filter, cancellationToken)),
                "invoice-status" => Results.Ok(await service.GetInvoiceStatusAsync(filter, cancellationToken)),
                _ => Results.NotFound(),
            };
        });
        group.MapGet("/{report}/export.csv", async (
            string report,
            [AsParameters] ReportingQueryParameters query,
            IReportingService service,
            CancellationToken cancellationToken) =>
        {
            if (!ReportNames.Contains(report))
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "Report not found", detail: "The requested report is not available.",
                    extensions: new Dictionary<string, object?> { ["code"] = "report.not_found" });
            }
            if (query.PageSize is > 100)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Page size is too large", detail: "Synchronous exports are limited to 100 rows per request.",
                    extensions: new Dictionary<string, object?> { ["code"] = "report.page_size_exceeded" });
            }

            try
            {
                var bytes = await service.ExportCsvAsync(report, query.ToFilter(), cancellationToken);
                return Results.File(bytes, "text/csv", $"loadix-{report.ToLowerInvariant()}.csv");
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Export unavailable", detail: "This report does not support CSV export yet.",
                    extensions: new Dictionary<string, object?> { ["code"] = "report.export_unavailable" });
            }
        });
        group.MapPost("/{report}/export-jobs", async (
            string report,
            [AsParameters] ReportingQueryParameters query,
            IReportExportService queue,
            ICurrentRequestContext requestContext,
            CancellationToken cancellationToken) =>
        {
            if (!ReportNames.Contains(report) || requestContext.TenantId is not { } tenantId)
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: "Report not found", detail: "The requested report is not available.",
                    extensions: new Dictionary<string, object?> { ["code"] = "report.not_found" });
            }

            var job = await queue.EnqueueAsync(report, query.ToFilter() with { PageSize = 5_000 }, tenantId, requestContext.UserId, cancellationToken);
            return Results.Accepted($"/api/v1/reports/export-jobs/{job.JobId:D}", job);
        });
        group.MapGet("/export-jobs/{jobId:guid}", (
            Guid jobId,
            IReportExportService queue,
            ICurrentRequestContext requestContext) => requestContext.TenantId is not { } tenantId
                ? Results.NotFound()
                : queue.GetJob(jobId, tenantId) is { } job ? Results.Ok(job) : Results.NotFound());
        group.MapGet("/export-jobs/{jobId:guid}/download", (
            Guid jobId,
            IReportExportService queue,
            ICurrentRequestContext requestContext) =>
        {
            if (requestContext.TenantId is not { } tenantId || queue.GetJob(jobId, tenantId) is not { } job)
            {
                return Results.NotFound();
            }

            var content = queue.GetContent(jobId, tenantId);
            return content is null
                ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Export is not ready")
                : Results.File(content, "text/csv", $"loadix-{job.Report.ToLowerInvariant()}.csv");
        });
        return group;
    }
}

public sealed class ReportingQueryParameters
{
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CustomerId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public ReportingFilter ToFilter() => new(
        FromUtc?.ToUniversalTime(),
        ToUtc?.ToUniversalTime(),
        BranchId,
        CustomerId,
        Math.Max(Page ?? 1, 1),
        Math.Clamp(PageSize ?? 50, 1, 100));
}
