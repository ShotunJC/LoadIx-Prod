using System.Threading.RateLimiting;
using LoadIx.Infrastructure.Edge;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoadIx.Api.Security;

public sealed class EdgeDeviceRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter;

    public EdgeDeviceRateLimiter(IOptions<EdgeDeviceOptions> options)
    {
        var configured = options.Value;
        limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configured.AuthenticatedRequestPermitLimit,
                    Window = TimeSpan.FromSeconds(configured.AuthenticatedRequestWindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }

    public ValueTask<RateLimitLease> AcquireAsync(
        string partition,
        CancellationToken cancellationToken) =>
        limiter.AcquireAsync(partition, 1, cancellationToken);

    public void Dispose() => limiter.Dispose();
}

public sealed class EdgeDeviceRateLimitingMiddleware(
    RequestDelegate next,
    EdgeDeviceRateLimiter limiter,
    ILogger<EdgeDeviceRateLimitingMiddleware> logger)
{
    private static readonly Action<ILogger, string, Exception?> RateLimitRejected =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4201, "EdgeRateLimitRejected"),
            "Edge device request rate limit rejected for partition {Partition}.");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/edge"))
        {
            await next(context);
            return;
        }

        // Device identifiers are attacker-controlled until authentication succeeds.
        // Keying this boundary by that header would let a caller evade the expensive
        // credential-verification limit by sending a fresh GUID on every request.
        var rawPartition = "unauthenticated:ip:"
            + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (!await TryAcquireAsync(context, rawPartition))
        {
            return;
        }

        var requiresEdgeAuthentication = !context.Request.Path.Equals(
            "/api/v1/edge/bootstrap",
            StringComparison.OrdinalIgnoreCase);
        if (requiresEdgeAuthentication)
        {
            var authentication = await context.AuthenticateAsync(
                EdgeDeviceAuthenticationDefaults.Scheme);
            if (authentication.Succeeded && authentication.Principal is not null)
            {
                context.User = authentication.Principal;
                var tenantId = context.User.FindFirst(LoadIxClaimTypes.TenantId)?.Value;
                var deviceId = context.User.FindFirst(LoadIxClaimTypes.EdgeDeviceId)?.Value;
                if (!string.IsNullOrWhiteSpace(tenantId)
                    && !string.IsNullOrWhiteSpace(deviceId)
                    && !await TryAcquireAsync(
                        context,
                        $"authenticated:{tenantId}:{deviceId}"))
                {
                    return;
                }
            }
        }

        await next(context);
    }

    private async Task<bool> TryAcquireAsync(HttpContext context, string partition)
    {
        using var lease = await limiter.AcquireAsync(partition, context.RequestAborted);
        if (lease.IsAcquired)
        {
            return true;
        }

        RateLimitRejected(logger, partition, null);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = "60";
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "The edge device request rate limit has been exceeded.",
                Type = "https://httpstatuses.com/429",
                Instance = context.Request.Path,
            },
            context.RequestAborted);
        return false;
    }
}
