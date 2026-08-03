using System.Threading.RateLimiting;
using LoadIx.Application.Edge;
using LoadIx.Infrastructure.Edge;
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

    public async Task InvokeAsync(
        HttpContext context,
        IEdgeDeviceRequestContext edgeRequestContext)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/edge"))
        {
            await next(context);
            return;
        }

        var rawDevicePartition = Guid.TryParse(
            context.Request.Headers[EdgeDeviceAuthenticationDefaults.DeviceIdHeader],
            out var rawDeviceId)
            ? rawDeviceId.ToString()
            : "invalid-device";
        var partition = edgeRequestContext.EdgeDeviceId is { } deviceId
            && edgeRequestContext.TenantId is { } tenantId
            ? $"authenticated:{tenantId.Value}:{deviceId}"
            : $"unauthenticated:{rawDevicePartition}:"
                + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        using var lease = await limiter.AcquireAsync(
            partition,
            context.RequestAborted);
        if (lease.IsAcquired)
        {
            await next(context);
            return;
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
    }
}
