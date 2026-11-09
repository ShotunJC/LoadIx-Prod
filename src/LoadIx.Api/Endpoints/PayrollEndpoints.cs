using LoadIx.Application.Finance;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;

namespace LoadIx.Api.Endpoints;

public static class PayrollEndpoints
{
    public static RouteGroupBuilder MapPayrollEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/payroll")
            .WithTags("Payroll Integration")
            .RequireAuthorization();

        group.MapPost("/batches/preview", async (
            PayrollBatchPreviewRequest request,
            IPayrollService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.PreviewBatchAsync(request, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinancePayrollView);

        group.MapPost("/batches", async (
            PayrollBatchCreateModel model,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBatchAsync(model, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/payroll/batches/{result.Value!.Id}", result.Value)
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinancePayrollManage);

        group.MapGet("/batches", async (
            IPayrollService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListBatchesAsync(cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinancePayrollView);

        group.MapGet("/batches/{id:guid}", async (
            Guid id,
            IPayrollService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetBatchAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinancePayrollView);

        group.MapPost("/batches/{id:guid}/approve", async (
            Guid id,
            IPayrollService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ApproveBatchAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinancePayrollManage);

        group.MapPost("/batches/{id:guid}/mark-exported", async (
            Guid id,
            IPayrollService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.MarkExportedAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FinancePayrollManage);

        group.MapGet("/batches/{id:guid}/export-pastel.csv", async (
            Guid id,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportPastelPayrollCsvAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!,
                    "text/csv",
                    $"pastel-payroll-batch-{id:N}.csv")
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinancePayrollView);

        group.MapGet("/batches/{id:guid}/export.csv", async (
            Guid id,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportGenericPayrollCsvAsync(id, cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!,
                    "text/csv",
                    $"payroll-deductions-batch-{id:N}.csv")
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FinancePayrollView);

        return api;
    }

    private static IResult ToResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var code = result.Error!.Code;
        var status = code switch
        {
            _ when code.Contains("not_found", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            _ when code.Contains("denied", StringComparison.OrdinalIgnoreCase)
                || code.Contains("authorization", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            _ when code.Contains("conflict", StringComparison.OrdinalIgnoreCase)
                || code.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || code.Contains("locked", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            statusCode: status,
            title: code,
            detail: result.Error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
    }
}
