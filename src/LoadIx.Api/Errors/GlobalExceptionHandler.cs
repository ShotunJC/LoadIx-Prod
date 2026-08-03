using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, string, Exception?>
        LogUnhandledException = LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(1, nameof(GlobalExceptionHandler)),
            "Unhandled {ExceptionType} while processing {Method} {Path}");
    private static readonly Action<ILogger, string, string, Exception?>
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
                null);
        }
        else
        {
            LogUnhandledException(
                logger,
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                null);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            // The public response is intentionally independent of exception
            // details; the trace ID is the safe support correlation key.
            Exception = null,
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
