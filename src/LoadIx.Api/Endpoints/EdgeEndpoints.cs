using System.Globalization;
using LoadIx.Api.Security;
using LoadIx.Application.Edge;
using LoadIx.Application.Security;
using LoadIx.Contracts.Edge;
using LoadIx.Domain.Common;

namespace LoadIx.Api.Endpoints;

public static class EdgeEndpoints
{
    public static RouteGroupBuilder MapEdgeEndpoints(this RouteGroupBuilder api)
    {
        MapAdminEndpoints(api.MapGroup("/edge-devices"));
        MapDeviceEndpoints(api.MapGroup("/edge"));
        return api;
    }

    private static void MapAdminEndpoints(RouteGroupBuilder group)
    {
        group.WithTags("Edge devices").RequireAuthorization();
        group.MapGet("/", async (
            [AsParameters] EdgeDeviceQueryParameters query,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListAsync(query.ToQuery(), cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceView);
        group.MapGet("/sites", async (
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.ListSitesAsync(cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceManage);
        group.MapPost("/registration-bootstrap", async (
            CreateEdgeDeviceBootstrapRequest request,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRegistrationBootstrapAsync(
                new EdgeRegistrationBootstrapModel(
                    request.Name,
                    request.SiteId,
                    request.WeighbridgeName,
                    request.WeighbridgeCode,
                    request.Protocol,
                    request.ParserVersion),
                cancellationToken);
            return result.IsSuccess
                ? Results.Created(
                    $"/api/v1/edge-devices/{result.Value!.DeviceId}",
                    result.Value)
                : ToResult(result);
        }).RequireAuthorization(LoadIxPermissions.EdgeDeviceManage);
        group.MapPost("/{deviceId:guid}/credentials/rotate", async (
            Guid deviceId,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.RotateCredentialAsync(
                deviceId,
                null,
                cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceManage);
        group.MapPost("/{deviceId:guid}/revoke", async (
            Guid deviceId,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.RevokeAsync(deviceId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceManage);
        group.MapGet("/{deviceId:guid}/diagnostics", async (
            Guid deviceId,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetDiagnosticsAsync(deviceId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceView);
        group.MapGet("/{deviceId:guid}/live-reading", async (
            Guid deviceId,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            ToResult(await service.GetLiveReadingAsync(deviceId, cancellationToken)))
            .RequireAuthorization(LoadIxPermissions.EdgeDeviceView);
    }

    private static void MapDeviceEndpoints(RouteGroupBuilder group)
    {
        group.WithTags("Edge agent");
        group.MapPost("/bootstrap", async (
            RegisterEdgeDeviceRequest request,
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            return ToResult(await service.RegisterAsync(
                new EdgeRegistrationModel(
                    request.DeviceId,
                    request.BootstrapToken,
                    request.AgentVersion),
                envelope,
                cancellationToken));
        })
        .RequireRateLimiting("edge-bootstrap");

        var authenticated = group
            .MapGroup("")
            .RequireAuthorization(EdgeDeviceAuthenticationDefaults.Policy)
            .RequireRateLimiting("edge-device");
        authenticated.MapPost("/credential/rotate", async (
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
            await WithEnvelopeAsync(
                httpContext,
                service.RotateCredentialAsync,
                cancellationToken));
        authenticated.MapPost("/heartbeat", async (
            EdgeHeartbeatRequest request,
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            return ToResult(await service.SubmitHeartbeatAsync(
                new EdgeHeartbeatModel(
                    request.DeviceTimestampUtc,
                    request.AgentVersion,
                    request.Status,
                    request.Metadata),
                envelope,
                cancellationToken));
        });
        authenticated.MapGet("/configuration", async (
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            return ToResult(await service.GetConfigurationAsync(
                envelope,
                cancellationToken));
        });
        authenticated.MapPost("/readings", async (
            EdgeWeightReadingRequest request,
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            if (!TryParseUnit(request.Unit, out var unit))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid weight unit",
                    detail: "Unit must be kg/kilograms or t/tonnes.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "edge.reading.unit_invalid",
                    });
            }

            return ToResult(await service.SubmitReadingAsync(
                new EdgeReadingModel(
                    request.WeighbridgeDeviceId,
                    request.DisplayedValue,
                    unit,
                    request.IsStable,
                    request.CapturedAtUtc,
                    request.VehicleRegistration,
                    request.SerialMessageMetadata,
                    request.ParserVersion,
                    request.CorrelationId),
                envelope,
                cancellationToken));
        });
        authenticated.MapPost("/tickets", async (
            EdgeTicketSubmissionRequest request,
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            return ToResult(await service.SubmitTicketAsync(
                new EdgeTicketModel(
                    request.WeighbridgeDeviceId,
                    request.TicketNumber,
                    request.CapturedAtUtc,
                    request.Content,
                    request.ContentType,
                    request.Metadata,
                    request.CorrelationId),
                envelope,
                cancellationToken));
        });
        authenticated.MapGet("/diagnostic-status", async (
            HttpContext httpContext,
            IEdgeDeviceService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
                return failure!;
            return ToResult(await service.GetDiagnosticStatusAsync(
                envelope,
                cancellationToken));
        });
    }

    private static async Task<IResult> WithEnvelopeAsync(
        HttpContext httpContext,
        Func<Guid?, EdgeRequestEnvelope?, CancellationToken, Task<EdgeResult<EdgeCredentialIssued>>> action,
        CancellationToken cancellationToken)
    {
        if (!TryGetEnvelope(httpContext, out var envelope, out var failure))
            return failure!;
        return ToResult(await action(null, envelope, cancellationToken));
    }

    private static bool TryGetEnvelope(
        HttpContext httpContext,
        out EdgeRequestEnvelope envelope,
        out IResult? failure)
    {
        var request = httpContext.Request;
        var timestampValue = request.Headers[
            EdgeDeviceAuthenticationDefaults.TimestampHeader].ToString();
        var nonce = request.Headers[EdgeDeviceAuthenticationDefaults.NonceHeader].ToString();
        var idempotencyKey = request.Headers[
            EdgeDeviceAuthenticationDefaults.IdempotencyHeader].ToString();
        if (!long.TryParse(
                timestampValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixSeconds))
        {
            envelope = null!;
            failure = Problem(
                "edge.timestamp_invalid",
                "A unix timestamp header is required.");
            return false;
        }

        try
        {
            envelope = new EdgeRequestEnvelope(
                idempotencyKey,
                nonce,
                DateTimeOffset.FromUnixTimeSeconds(unixSeconds),
                httpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (ArgumentOutOfRangeException)
        {
            envelope = null!;
            failure = Problem(
                "edge.timestamp_invalid",
                "The request timestamp is invalid.");
            return false;
        }

        failure = null;
        return true;
    }

    private static bool TryParseUnit(string value, out WeightUnit unit)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "kg":
            case "kgs":
            case "kilogram":
            case "kilograms":
                unit = WeightUnit.Kilograms;
                return true;
            case "t":
            case "tonne":
            case "tonnes":
                unit = WeightUnit.Tonnes;
                return true;
            default:
                unit = default;
                return false;
        }
    }

    private static IResult ToResult<T>(EdgeResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);
        var status = result.Failure switch
        {
            EdgeFailure.Unauthorized => StatusCodes.Status401Unauthorized,
            EdgeFailure.Validation => StatusCodes.Status400BadRequest,
            EdgeFailure.Forbidden => StatusCodes.Status403Forbidden,
            EdgeFailure.NotFound => StatusCodes.Status404NotFound,
            EdgeFailure.Conflict => StatusCodes.Status409Conflict,
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

    private static IResult Problem(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid edge request",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
}

public sealed class EdgeDeviceQueryParameters
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Search { get; init; }

    public string? Sort { get; init; }

    public string? Direction { get; init; }

    public EdgeDeviceQuery ToQuery() => new(
        Page ?? 1,
        Math.Clamp(PageSize ?? 20, 1, 100),
        Search,
        Sort ?? "name",
        Direction?.ToLowerInvariant() ?? "asc");
}
