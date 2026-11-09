using LoadIx.Application.Fleet;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;

namespace LoadIx.Api.Endpoints;

public static class DriverDisciplinaryEndpoints
{
    public static RouteGroupBuilder MapDriverDisciplinaryEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/drivers/{driverId:guid}")
            .WithTags("Driver Disciplinary & Deductions")
            .RequireAuthorization();

        group.MapGet("/warnings", async (
            Guid driverId,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetDriverWarningsAsync(driverId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetWarningView);

        group.MapPost("/warnings", async (
            Guid driverId,
            DriverWarningCreateModel model,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.IssueWarningAsync(driverId, model, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/drivers/{driverId}/warnings", result.Value)
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FleetWarningManage);

        group.MapGet("/deductions", async (
            Guid driverId,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetDriverDeductionsAsync(driverId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetDeductionView);

        group.MapPost("/deductions", async (
            Guid driverId,
            DriverDeductionCreateModel model,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateDeductionAsync(driverId, model, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/drivers/{driverId}/deductions", result.Value)
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.FleetDeductionManage);

        group.MapGet("/disciplinary-summary", async (
            Guid driverId,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetDriverDisciplinarySummaryAsync(driverId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetWarningView);

        // Warning actions
        var warningGroup = api.MapGroup("/warnings/{id:guid}")
            .WithTags("Driver Disciplinary & Deductions")
            .RequireAuthorization();

        warningGroup.MapPost("/revoke", async (
            Guid id,
            RevokeWarningModel model,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.RevokeWarningAsync(id, model, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetWarningManage);

        // Deduction actions
        var deductionGroup = api.MapGroup("/deductions/{id:guid}")
            .WithTags("Driver Disciplinary & Deductions")
            .RequireAuthorization();

        deductionGroup.MapPost("/approve", async (
            Guid id,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ApproveDeductionAsync(id, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetDeductionManage);

        deductionGroup.MapPost("/cancel", async (
            Guid id,
            CancelDeductionModel model,
            IDriverDisciplinaryService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.CancelDeductionAsync(id, model, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.FleetDeductionManage);

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
