using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using Microsoft.Extensions.Primitives;

namespace LoadIx.Api.Endpoints;

public static class TripEndpoints
{
    public static RouteGroupBuilder MapTripEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/trips").WithTags("Trips");
        group.MapGet("/", async (
            [AsParameters] TripQueryParameters query,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.TripView);
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.TripView);
        group.MapPost("/", async (
            CreateTripModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.CreateAsync(
                    model, IdempotencyKey(context), cancellationToken),
                context,
                true))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPost("/{id:guid}/assign", (
            Guid id,
            AssignTripModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            Assignment(
                id, model, false, context, service, cancellationToken))
            .RequireAuthorization(LoadIxPermissions.TripManage);
        group.MapPost("/{id:guid}/reassign", (
            Guid id,
            AssignTripModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            Assignment(
                id, model, true, context, service, cancellationToken))
            .RequireAuthorization(LoadIxPermissions.TripManage);

        MapCommand(
            group, "dispatch", "DispatchTrip", LoadIxPermissions.TripDispatch);
        MapCommand(
            group,
            "collection-arrival",
            "ConfirmCollectionArrival",
            LoadIxPermissions.TripManage);
        MapCommand(
            group, "start-loading", "StartLoading", LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "complete-collection",
            "CompleteCollection",
            LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "depart-collection",
            "DepartCollection",
            LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "delivery-arrival",
            "ConfirmDeliveryArrival",
            LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "start-unloading",
            "StartUnloading",
            LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "complete-delivery",
            "CompleteDelivery",
            LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "submit-reconciliation",
            "SubmitForReconciliation",
            LoadIxPermissions.TripManage);
        MapCommand(
            group, "complete", "CompleteTrip", LoadIxPermissions.TripComplete);
        MapCommand(
            group, "cancel", "CancelTrip", LoadIxPermissions.TripManage);
        MapCommand(
            group, "reject", "RejectTrip", LoadIxPermissions.TripManage);
        MapCommand(
            group,
            "confirm-reconciliation",
            "ConfirmReconciliation",
            LoadIxPermissions.TripComplete);
        MapCommand(
            group,
            "confirm-required-documents",
            "ConfirmRequiredDocuments",
            LoadIxPermissions.TripComplete);

        group.MapPost("/{id:guid}/override", async (
            Guid id,
            TripOverrideModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.OverrideAsync(
                    id,
                    model,
                    IdempotencyKey(context),
                    cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TripOverride);
        group.MapPost("/{id:guid}/source-weighbridge", (
            Guid id,
            TripWeighbridgeModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            Weighbridge(
                id, true, model, context, service, cancellationToken))
            .RequireAuthorization();
        group.MapPost("/{id:guid}/destination-weighbridge", (
            Guid id,
            TripWeighbridgeModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            Weighbridge(
                id, false, model, context, service, cancellationToken))
            .RequireAuthorization();
        group.MapPost("/{id:guid}/manual-quantity", async (
            Guid id,
            TripManualQuantityModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.RecordManualQuantityAsync(
                    id, model, IdempotencyKey(context), cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TripOverride);
        group.MapPost("/{id:guid}/notes", async (
            Guid id,
            TripNoteModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.AddNoteAsync(
                    id,
                    model,
                    IdempotencyKey(context),
                    cancellationToken),
                context))
            .RequireAuthorization();
        group.MapPost("/{id:guid}/location", async (
            Guid id,
            TripLocationUpdateModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.RecordLocationAsync(
                    id,
                    model,
                    IdempotencyKey(context),
                    cancellationToken),
                context))
            .RequireAuthorization();
        return api;
    }

    private static void MapCommand(
        RouteGroupBuilder group,
        string route,
        string command,
        string permission) =>
        group.MapPost($"/{{id:guid}}/{route}", async (
            Guid id,
            TripCommandModel model,
            HttpContext context,
            ITripService service,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.ExecuteAsync(
                    id,
                    command,
                    model,
                    IdempotencyKey(context),
                    cancellationToken),
                context))
            .RequireAuthorization();

    private static async Task<IResult> Assignment(
        Guid id,
        AssignTripModel model,
        bool reassign,
        HttpContext context,
        ITripService service,
        CancellationToken cancellationToken) =>
        ToItemResult(
            await service.AssignAsync(
                id,
                model,
                reassign,
                IdempotencyKey(context),
                cancellationToken),
            context);

    private static async Task<IResult> Weighbridge(
        Guid id,
        bool source,
        TripWeighbridgeModel model,
        HttpContext context,
        ITripService service,
        CancellationToken cancellationToken) =>
        ToItemResult(
            await service.RecordWeighbridgeAsync(
                id,
                source,
                model,
                IdempotencyKey(context),
                cancellationToken),
            context);

    private static IResult ToItemResult(
        TripResult<TripDetail> result,
        HttpContext context,
        bool created = false)
    {
        if (!result.IsSuccess) return ToResult(result);
        context.Response.Headers.ETag = $"\"{result.Value!.Version}\"";
        return created
            ? Results.Created($"/api/v1/trips/{result.Value.Id}", result.Value)
            : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(TripResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            TripFailure.Validation => StatusCodes.Status400BadRequest,
            TripFailure.Forbidden => StatusCodes.Status403Forbidden,
            TripFailure.NotFound => StatusCodes.Status404NotFound,
            TripFailure.Conflict => StatusCodes.Status409Conflict,
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

    private static string IdempotencyKey(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            "Idempotency-Key", out StringValues key);
        return key.ToString();
    }
}

public sealed class TripQueryParameters
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? DriverId { get; init; }
    public Guid? VehicleId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }

    public TripQuery ToQuery() =>
        new(
            Page ?? 1,
            PageSize ?? 20,
            Search,
            Status,
            CustomerId,
            DriverId,
            VehicleId,
            FromUtc,
            ToUtc,
            Sort ?? "collection",
            Direction ?? "asc");
}
