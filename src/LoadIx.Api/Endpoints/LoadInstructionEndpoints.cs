using LoadIx.Application.Instructions;
using LoadIx.Application.Security;
using Microsoft.Extensions.Primitives;

namespace LoadIx.Api.Endpoints;

public static class LoadInstructionEndpoints
{
    public static RouteGroupBuilder MapLoadInstructionEndpoints(
        this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/load-instructions")
            .WithTags("Load instructions");
        group.MapGet("/", async (
            [AsParameters] InstructionQueryParameters query,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.TripView);
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.TripView);
        group.MapPost("/", async (
            InstructionWriteModel model,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.CreateAsync(model, cancellationToken),
                context,
                created: true))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPut("/{id:guid}", async (
            Guid id,
            InstructionWriteModel model,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.UpdateAsync(id, model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPost("/{id:guid}/plan", async (
            Guid id,
            InstructionTransitionModel model,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.PlanAsync(id, model.Version, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            InstructionCancellationModel model,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.CancelAsync(id, model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPost("/{id:guid}/generate-trips", async (
            Guid id,
            GenerateTripsModel model,
            HttpContext context,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
        {
            context.Request.Headers.TryGetValue(
                "Idempotency-Key", out StringValues key);
            return ToItemResult(
                await service.GenerateTripsAsync(
                    id,
                    model,
                    key.ToString(),
                    cancellationToken),
                context);
        })
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapGet("/{id:guid}/history", async (
            Guid id,
            ILoadInstructionService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.HistoryAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.TripView);
        return api;
    }

    private static IResult ToItemResult(
        InstructionResult<InstructionDetail> result,
        HttpContext context,
        bool created = false)
    {
        if (!result.IsSuccess) return ToResult(result);
        context.Response.Headers.ETag = $"\"{result.Value!.Version}\"";
        return created
            ? Results.Created(
                $"/api/v1/load-instructions/{result.Value.Id}",
                result.Value)
            : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(InstructionResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            InstructionFailure.Validation => StatusCodes.Status400BadRequest,
            InstructionFailure.Forbidden => StatusCodes.Status403Forbidden,
            InstructionFailure.NotFound => StatusCodes.Status404NotFound,
            InstructionFailure.Conflict => StatusCodes.Status409Conflict,
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

public sealed class InstructionQueryParameters
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ContractId { get; init; }
    public string? Priority { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }

    public InstructionQuery ToQuery() =>
        new(
            Page ?? 1,
            PageSize ?? 20,
            Search,
            Status,
            CustomerId,
            ContractId,
            Priority,
            Sort ?? "collectionWindow",
            Direction ?? "asc");
}
