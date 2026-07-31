using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, Exception>
        LogUnhandledException = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1, nameof(GlobalExceptionHandler)),
            "Unhandled exception while processing {Method} {Path}");
    private static readonly Action<ILogger, string, string, Exception>
        LogConcurrencyConflict = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, "ConcurrencyConflict"),
            "Concurrency conflict while processing {Method} {Path}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isConcurrencyConflict = exception is DbUpdateConcurrencyException;
        var statusCode = isConcurrencyConflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        var title = isConcurrencyConflict
            ? "The record was changed by another operation."
            : "An unexpected error occurred.";
        var code = isConcurrencyConflict
            ? "concurrency_conflict"
            : "unexpected_error";

        if (isConcurrencyConflict)
        {
            LogConcurrencyConflict(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception);
        }
        else
        {
            LogUnhandledException(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = $"https://httpstatuses.com/{statusCode}",
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = httpContext.TraceIdentifier,
                },
            },
        });
    }
}
