using LoadIx.Application.MasterData;
using LoadIx.Application.Security;

namespace LoadIx.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static RouteGroupBuilder MapMasterDataEndpoints(this RouteGroupBuilder api)
    {
        MapBranches(api.MapGroup("/branches").WithTags("Branches"));
        MapCustomers(api.MapGroup("/customers").WithTags("Customers"));
        MapLocations(api.MapGroup("/locations").WithTags("Locations"));
        MapCommodities(api.MapGroup("/commodities").WithTags("Commodities"));
        MapDrivers(api.MapGroup("/drivers").WithTags("Drivers"));
        MapVehicles(api.MapGroup("/vehicles").WithTags("Vehicles"));
        MapTrailers(api.MapGroup("/trailers").WithTags("Trailers"));
        return api;
    }

    private static void MapBranches(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListBranchesAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.TenantView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetBranchAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.TenantView);
        group.MapPost("/", async (
            BranchWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateBranchAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.TenantManage);
        group.MapPut("/{id:guid}", async (
            Guid id, BranchWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateBranchAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.TenantManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetBranchActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.TenantManage);
    }

    private static void MapCustomers(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListCustomersAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.CustomerView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetCustomerAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerView);
        group.MapPost("/", async (
            CustomerWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateCustomerAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
        group.MapPut("/{id:guid}", async (
            Guid id, CustomerWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateCustomerAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetCustomerActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
    }

    private static void MapLocations(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListLocationsAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.CustomerView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetLocationAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerView);
        group.MapPost("/", async (
            LocationWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateLocationAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
        group.MapPut("/{id:guid}", async (
            Guid id, LocationWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateLocationAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetLocationActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.CustomerManage);
    }

    private static void MapCommodities(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListCommoditiesAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetCommodityAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.ContractView);
        group.MapPost("/", async (
            CommodityWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateCommodityAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPut("/{id:guid}", async (
            Guid id, CommodityWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateCommodityAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetCommodityActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.ContractManage);
    }

    private static void MapDrivers(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListDriversAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetDriverAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapPost("/", async (
            DriverWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateDriverAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPut("/{id:guid}", async (
            Guid id, DriverWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateDriverAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetDriverActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
    }

    private static void MapVehicles(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListVehiclesAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetVehicleAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapPost("/", async (
            VehicleWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateVehicleAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPut("/{id:guid}", async (
            Guid id, VehicleWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateVehicleAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetVehicleActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
    }

    private static void MapTrailers(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            [AsParameters] MasterDataQueryParameters query,
            IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListTrailersAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.GetTrailerAsync(id, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetView);
        group.MapPost("/", async (
            TrailerWriteModel model, HttpContext context, IMasterDataService service,
            CancellationToken cancellationToken) =>
            ToItemResult(await service.CreateTrailerAsync(model, cancellationToken), context, true))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPut("/{id:guid}", async (
            Guid id, TrailerWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.UpdateTrailerAsync(id, model, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
        group.MapPost("/{id:guid}/status", async (
            Guid id, StatusWriteModel model, HttpContext context,
            IMasterDataService service, CancellationToken cancellationToken) =>
            ToItemResult(await service.SetTrailerActiveAsync(
                id, model.IsActive, model.Version, cancellationToken), context))
            .RequireAuthorization(LoadIxPermissions.FleetManage);
    }

    private static IResult ToItemResult<T>(
        MasterDataResult<T> result,
        HttpContext context,
        bool created = false)
        where T : MasterDataItem
    {
        if (!result.IsSuccess) return ToResult(result);
        context.Response.Headers.ETag = $"\"{result.Value!.Version}\"";
        return created
            ? Results.Created($"{context.Request.Path}/{result.Value.Id}", result.Value)
            : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(MasterDataResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            MasterDataFailure.Validation => StatusCodes.Status400BadRequest,
            MasterDataFailure.Forbidden => StatusCodes.Status403Forbidden,
            MasterDataFailure.NotFound => StatusCodes.Status404NotFound,
            MasterDataFailure.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = result.Code,
        };
        if (result.Errors is not null) extensions["errors"] = result.Errors;
        return Results.Problem(
            statusCode: status,
            title: result.Failure.ToString(),
            detail: result.Message,
            extensions: extensions);
    }
}

public sealed class MasterDataQueryParameters
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public string? Direction { get; init; }
    public string? Status { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? CustomerId { get; init; }

    public MasterDataQuery ToQuery() =>
        new(
            Page ?? 1,
            PageSize ?? 20,
            Search,
            Sort,
            Direction ?? "asc",
            Status ?? "active",
            BranchId,
            CustomerId);
}

public sealed record StatusWriteModel(bool IsActive, string Version);
