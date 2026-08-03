using LoadIx.Application.Governance;
using LoadIx.Application.Security;

namespace LoadIx.Api.Endpoints;

public static class GovernanceEndpoints
{
    public static RouteGroupBuilder MapGovernanceEndpoints(
        this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/governance")
            .WithTags("Data governance")
            .RequireAuthorization();

        group.MapGet("/retention-policy", async (
            IDataGovernanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetRetentionPolicyAsync(cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.TenantManage);
        group.MapPut("/retention-policy", async (
            RetentionPolicyUpdateModel model,
            IDataGovernanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.UpdateRetentionPolicyAsync(model, cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.TenantManage);
        group.MapGet("/personal-data-inventory", (
            IDataGovernanceService service) => Results.Ok(service.GetPersonalDataInventory()))
            .RequireAuthorization(LoadIxPermissions.TenantView);
        group.MapPost("/tenant-export", async (
            IDataGovernanceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ExportTenantDataAsync(cancellationToken);
            return result.IsSuccess
                ? Results.File(
                    result.Value!.Content,
                    "application/json",
                    result.Value.FileName)
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.TenantDataExport);
        group.MapPost("/anonymisation-requests", async (
            AnonymisationRequestModel model,
            IDataGovernanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.RequestAnonymisationAsync(model, cancellationToken),
                context,
                true))
            .RequireAuthorization(LoadIxPermissions.UserManage);
        group.MapGet("/anonymisation-requests", async (
            IDataGovernanceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListAnonymisationRequestsAsync(cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.UserManage);
        group.MapPost("/anonymisation-requests/{requestId:guid}/approve", async (
            Guid requestId,
            AnonymisationApprovalModel model,
            IDataGovernanceService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ToItemResult(
                await service.ApproveAnonymisationAsync(
                    requestId,
                    model,
                    cancellationToken),
                context))
            .RequireAuthorization(LoadIxPermissions.UserManage);

        return api;
    }

    private static IResult ToItemResult<T>(
        GovernanceResult<T> result,
        HttpContext context,
        bool created = false)
    {
        if (!result.IsSuccess)
        {
            return ToResult(result);
        }

        var version = typeof(T).GetProperty("Version")?.GetValue(result.Value);
        if (version is string text && !string.IsNullOrWhiteSpace(text))
        {
            context.Response.Headers.ETag = $"\"{text}\"";
        }

        return created
            ? Results.Created(string.Empty, result.Value)
            : Results.Ok(result.Value);
    }

    private static IResult ToResult<T>(GovernanceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var status = result.Failure switch
        {
            GovernanceFailure.Validation => StatusCodes.Status400BadRequest,
            GovernanceFailure.Forbidden => StatusCodes.Status403Forbidden,
            GovernanceFailure.NotFound => StatusCodes.Status404NotFound,
            GovernanceFailure.Conflict => StatusCodes.Status409Conflict,
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
