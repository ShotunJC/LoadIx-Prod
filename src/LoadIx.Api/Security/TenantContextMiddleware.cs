using System.IdentityModel.Tokens.Jwt;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Security;

public sealed class TenantContextMiddleware(
    CurrentRequestContext requestContext,
    LoadIxDbContext dbContext,
    SecurityAuditWriter auditWriter,
    ILogger<TenantContextMiddleware> logger) : IMiddleware
{
    public const string SupportTenantHeader = "X-LoadIx-Support-Tenant";
    public const string SupportContextHeader = "X-LoadIx-Support-Context";

    public async Task InvokeAsync(
        HttpContext context,
        RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = ParseGuid(
            context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        var claimedTenantId = ParseGuid(
            context.User.FindFirst(LoadIxClaimTypes.TenantId)?.Value);
        var isPlatformAdministrator = context.User.HasClaim(
            LoadIxClaimTypes.PlatformAdministrator,
            bool.TrueString);
        var supportHeader = context.Request.Headers[SupportTenantHeader].ToString();

        TenantId? effectiveTenantId = claimedTenantId.HasValue
            ? TenantId.From(claimedTenantId.Value)
            : null;
        var isSupportContext = false;

        if (!string.IsNullOrWhiteSpace(supportHeader))
        {
            if (!isPlatformAdministrator
                || !Guid.TryParse(supportHeader, out var supportTenantValue))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var supportTenantId = TenantId.From(supportTenantValue);
            var tenantExists = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(
                    tenant => tenant.Id == supportTenantId && tenant.IsActive,
                    context.RequestAborted);
            if (!tenantExists)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            effectiveTenantId = supportTenantId;
            isSupportContext = true;
            context.Response.Headers[SupportContextHeader] =
                supportTenantId.Value.ToString();
            await auditWriter.WriteAsync(
                "SupportContext.Entered",
                "Succeeded",
                "Tenant",
                supportTenantId.Value.ToString(),
                supportTenantId,
                userId,
                true,
                context.Connection.RemoteIpAddress?.ToString(),
                context.RequestAborted);
        }

        if (effectiveTenantId.HasValue)
        {
            requestContext.Set(
                effectiveTenantId.Value,
                userId,
                isSupportContext,
                context.User.FindAll(LoadIxClaimTypes.Permission)
                    .Select(claim => claim.Value)
                    .Concat(isPlatformAdministrator
                        ? LoadIxPermissions.All
                        : []));
        }

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = effectiveTenantId?.Value,
            ["ActorUserId"] = userId,
            ["IsSupportContext"] = isSupportContext,
        }))
        {
            await next(context);
        }
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
