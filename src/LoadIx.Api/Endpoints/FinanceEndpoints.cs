using LoadIx.Application.Finance;
using LoadIx.Application.Security;

namespace LoadIx.Api.Endpoints;

public static class FinanceEndpoints
{
    public static RouteGroupBuilder MapFinanceEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/finance").WithTags("Finance");
        group.MapGet("/tax-configuration", async (
            IFinanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetTaxConfigurationAsync(cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapPut("/tax-configuration", async (
            TenantTaxConfigurationWriteModel model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.UpdateTaxConfigurationAsync(model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);

        group.MapGet("/trips/{tripId:guid}", async (
            Guid tripId,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.GetTripFinancialsAsync(tripId, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapPost("/trips/{tripId:guid}/recalculate", async (
            Guid tripId,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.RecalculateTripFinancialsAsync(tripId, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);
        group.MapPost("/trips/{tripId:guid}/costs", async (
            Guid tripId,
            CostEntryWriteModel model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.AddCostAsync(tripId, model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);
        group.MapPost("/trips/{tripId:guid}/approve", async (
            Guid tripId,
            ApproveFinancialsModel model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.ApproveTripFinancialsAsync(
                    tripId,
                    model,
                    cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);

        group.MapGet("/invoice-ready", async (
            [AsParameters] InvoiceReadyQueryParameters query,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListInvoiceReadyAsync(
                query.ToQuery(),
                cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapPost("/invoice-batches/preview", async (
            InvoiceBatchPreviewRequest model,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.PreviewInvoiceBatchAsync(
                model,
                cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapPost("/invoice-batches", async (
            InvoiceBatchCreateModel model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.CreateInvoiceBatchAsync(model, cancellationToken),
                context,
                true))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);
        group.MapGet("/invoice-batches", async (
            int? page,
            int? pageSize,
            IFinanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListInvoiceBatchesAsync(
                page ?? 1,
                pageSize ?? 20,
                cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapGet("/invoice-batches/{id:guid}", async (
            Guid id,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.GetInvoiceBatchAsync(id, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapPost("/invoice-batches/{id:guid}/approve", async (
            Guid id,
            VersionedFinanceCommand model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.ApproveInvoiceBatchAsync(
                    id,
                    model,
                    cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);
        group.MapPost("/invoice-batches/{id:guid}/mark-exported", async (
            Guid id,
            VersionedFinanceCommand model,
            IFinanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.MarkInvoiceBatchExportedAsync(
                    id,
                    model,
                    cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.FinanceManage);
        group.MapGet("/invoice-batches/{id:guid}/export.csv", async (
            Guid id,
            IFinanceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportInvoiceBatchCsvAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!,
                    "text/csv",
                    $"loadix-invoice-batch-{id:N}.csv")
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinanceView);
        group.MapGet("/invoice-batches/{id:guid}/summary.pdf", async (
            Guid id,
            IFinanceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportInvoiceBatchPdfAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!,
                    "application/pdf",
                    $"loadix-invoice-batch-{id:N}.pdf")
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinanceView);

        group.MapGet("/invoice-batches/{id:guid}/export-pastel.csv", async (
            Guid id,
            IFinanceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportInvoiceBatchPastelAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!,
                    "text/csv",
                    $"pastel-invoice-batch-{id:N}.csv")
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinanceView);

        return api;
    }

    private static IResult ToItemResult<T>(
        FinanceResult<T> result,
        HttpContext context,
        bool created = false)
    {
        if (!result.IsSuccess) return ToResult(result);
        var version = typeof(T).GetProperty("Version")?.GetValue(result.Value);
        if (version is string text) context.Response.Headers.ETag = $"\"{text}\"";
        return created ? Results.Created(string.Empty, result.Value) : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(FinanceResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            FinanceFailure.Validation => StatusCodes.Status400BadRequest,
            FinanceFailure.Forbidden => StatusCodes.Status403Forbidden,
            FinanceFailure.NotFound => StatusCodes.Status404NotFound,
            FinanceFailure.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Problem(
            statusCode: status,
            title: result.Failure.ToString(),
            detail: result.Message,
            extensions: new Dictionary<string, object?> { ["code"] = result.Code });
    }
}

public sealed class InvoiceReadyQueryParameters
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ContractId { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }

    public InvoiceReadyQuery ToQuery() =>
        new(
            Page ?? 1,
            PageSize ?? 20,
            Search,
            CustomerId,
            ContractId,
            Sort ?? "completed",
            Direction ?? "asc");
}
