using System.Diagnostics;

namespace LoadIx.Api.Observability;

public sealed class TraceCorrelationMiddleware(
    RequestDelegate next,
    ILogger<TraceCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
        var spanId = activity?.SpanId.ToString();
        context.Response.Headers["X-Trace-Id"] = traceId;
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["SpanId"] = spanId,
            ["RequestId"] = context.TraceIdentifier,
            ["Operation"] = $"{context.Request.Method} {context.Request.Path}",
        }))
        {
            await next(context);
        }
    }
}
