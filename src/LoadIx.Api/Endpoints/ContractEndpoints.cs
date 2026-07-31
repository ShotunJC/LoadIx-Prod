using LoadIx.Application.Contracts;
using LoadIx.Application.Security;

namespace LoadIx.Api.Endpoints;

public static class ContractEndpoints
{
    public static RouteGroupBuilder MapContractEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/contracts").WithTags("Transport contracts");
        group.MapGet("/", async (
            [AsParameters] ContractQueryParameters query,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        group.MapPost("/", async (
            ContractWriteModel model,
            HttpContext context,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.CreateAsync(model, cancellationToken),
                context,
                created: true))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPut("/{id:guid}", async (
            Guid id,
            ContractWriteModel model,
            HttpContext context,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.UpdateAsync(id, model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPost("/{id:guid}/activate", async (
            Guid id,
            ContractTransitionModel model,
            HttpContext context,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.ActivateAsync(id, model.Version, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPost("/{id:guid}/suspend", async (
            Guid id,
            ContractTransitionModel model,
            HttpContext context,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.SuspendAsync(id, model.Version, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPost("/{id:guid}/rate-preview", async (
            Guid id,
            RatePreviewModel model,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.PreviewRateAsync(id, model, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        group.MapGet("/{id:guid}/history", async (
            Guid id,
            IContractService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.HistoryAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        return api;
    }

    private static IResult ToItemResult(
        ContractResult<ContractDetail> result,
        HttpContext context,
        bool created = false)
    {
        if (!result.IsSuccess) return ToResult(result);
        context.Response.Headers.ETag = $"\"{result.Value!.Version}\"";
        return created
            ? Results.Created($"/api/v1/contracts/{result.Value.Id}", result.Value)
            : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(ContractResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            ContractFailure.Validation => StatusCodes.Status400BadRequest,
            ContractFailure.Forbidden => StatusCodes.Status403Forbidden,
            ContractFailure.NotFound => StatusCodes.Status404NotFound,
            ContractFailure.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Problem(
            statusCode: status,
            title: result.Failure.ToString(),
            detail: result.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.Code,
            });
    }
}

public sealed class ContractQueryParameters
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }

    public ContractQuery ToQuery() =>
        new(
            Page ?? 1,
            PageSize ?? 20,
            Search,
            Status,
            CustomerId,
            Sort ?? "contractNumber",
            Direction ?? "asc");
}
