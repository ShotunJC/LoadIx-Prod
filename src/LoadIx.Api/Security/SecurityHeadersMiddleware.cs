namespace LoadIx.Api.Security;

public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; object-src 'none'";
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

        if (!environment.IsDevelopment() && context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] =
                "max-age=31536000; includeSubDomains";
        }

        await next(context);
    }
}
