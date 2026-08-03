using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LoadIx.Api.Configuration;
using LoadIx.Api.Security;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoadIx.Api.Endpoints;

public static class UserAdminEndpoints
{
    public static RouteGroupBuilder MapUserAdminEndpoints(
        this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users")
            .WithTags("User administration")
            .RequireAuthorization();

        users.MapGet("/", ListUsersAsync)
            .RequireAuthorization(LoadIxPermissions.UserView);
        users.MapGet("/roles", ListRolesAsync)
            .RequireAuthorization(LoadIxPermissions.UserView);
        users.MapPost("/invitations", InviteUserAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage)
            .RequireRateLimiting("user-administration");
        users.MapPost("/invitations/{invitationId:guid}/resend", ResendInvitationAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage)
            .RequireRateLimiting("user-administration");
        users.MapPut("/{userId:guid}/roles", AssignRolesAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage);
        users.MapPut("/roles/{roleId:guid}/permissions", UpdateRolePermissionsAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage);
        users.MapPost("/{userId:guid}/deactivate", DeactivateUserAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage);
        users.MapGet("/{userId:guid}/sessions", ListUserSessionsAsync)
            .RequireAuthorization(LoadIxPermissions.UserView);
        users.MapDelete("/{userId:guid}/sessions/{sessionId:guid}", RevokeUserSessionAsync)
            .RequireAuthorization(LoadIxPermissions.UserManage);

        api.MapGet("/audit-events", ListAuditEventsAsync)
            .WithTags("Audit")
            .RequireAuthorization(LoadIxPermissions.AuditView);
        return api;
    }

    private static async Task<IResult> ListUsersAsync(
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var users = await dbContext.Users
            .Where(user => user.TenantId == tenantId)
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.IsActive,
                user.EmailConfirmed,
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        dbContext.Roles.Where(role => role.TenantId == tenantId),
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (_, role) => new UserRoleSummary(role.Id, role.DisplayName))
                    .ToArray()))
            .ToListAsync(cancellationToken);
        return Results.Ok(users);
    }

    private static async Task<IResult> ListRolesAsync(
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var roles = await dbContext.Roles
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.DisplayName)
            .Select(role => new RoleResponse(
                role.Id,
                role.DisplayName,
                role.Permissions.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries)))
            .ToListAsync(cancellationToken);
        return Results.Ok(roles);
    }

    private static async Task<IResult> InviteUserAsync(
        InviteUserRequest request,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        IdentityTokenService tokenService,
        IAccountEmailSender emailSender,
        SecurityAuditWriter auditWriter,
        IOptions<LoadIxOptions> loadIxOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var roles = await AuthorizedRolesAsync(
            request.RoleIds ?? [],
            tenantId,
            httpContext.User,
            dbContext,
            cancellationToken);
        if (roles is null)
        {
            return AuthorityExceeded();
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(
                user => user.TenantId == tenantId
                    && user.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return Results.Conflict(new GenericMessageResponse(
                "A user or pending invitation already exists."));
        }

        var existingInvitation = await dbContext.UserInvitations.AnyAsync(
            invitation => invitation.TenantId == tenantId
                && invitation.NormalizedEmail == normalizedEmail
                && invitation.AcceptedAtUtc == null
                && invitation.RevokedAtUtc == null,
            cancellationToken);
        if (existingInvitation)
        {
            return Results.Conflict(new GenericMessageResponse(
                "A user or pending invitation already exists."));
        }

        var secret = tokenService.CreateInvitationToken();
        var now = timeProvider.GetUtcNow();
        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            TokenHash = secret.Hash,
            RoleIds = string.Join(',', roles.Select(role => role.Id)),
            InvitedByUserId = requestContext.UserId!.Value,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
        };
        dbContext.UserInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SendInvitationAsync(
            invitation,
            secret.RawToken,
            emailSender,
            loadIxOptions.Value.WebOrigin,
            cancellationToken);
        await auditWriter.WriteAsync(
            "User.Invited",
            "Succeeded",
            "Invitation",
            invitation.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken);
        return Results.Created(
            $"/api/v1/users/invitations/{invitation.Id}",
            new InvitationResponse(
                invitation.Id,
                invitation.Email,
                invitation.DisplayName,
                invitation.ExpiresAtUtc,
                roles.Select(role =>
                    new UserRoleSummary(role.Id, role.DisplayName)).ToArray()));
    }

    private static async Task<IResult> ResendInvitationAsync(
        Guid invitationId,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        IdentityTokenService tokenService,
        IAccountEmailSender emailSender,
        SecurityAuditWriter auditWriter,
        IOptions<LoadIxOptions> loadIxOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var invitation = await dbContext.UserInvitations.SingleOrDefaultAsync(
            candidate => candidate.Id == invitationId
                && candidate.TenantId == tenantId
                && candidate.AcceptedAtUtc == null
                && candidate.RevokedAtUtc == null,
            cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        var roles = await AuthorizedRolesAsync(
            invitation.RoleIds.Split(',').Select(Guid.Parse).ToArray(),
            tenantId,
            httpContext.User,
            dbContext,
            cancellationToken);
        if (roles is null)
        {
            return AuthorityExceeded();
        }

        var secret = tokenService.CreateInvitationToken();
        invitation.TokenHash = secret.Hash;
        invitation.CreatedAtUtc = timeProvider.GetUtcNow();
        invitation.ExpiresAtUtc = invitation.CreatedAtUtc.AddDays(7);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SendInvitationAsync(
            invitation,
            secret.RawToken,
            emailSender,
            loadIxOptions.Value.WebOrigin,
            cancellationToken);
        await auditWriter.WriteAsync(
            "User.InvitationResent",
            "Succeeded",
            "Invitation",
            invitation.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignRolesAsync(
        Guid userId,
        AssignRolesRequest request,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var roles = await AuthorizedRolesAsync(
            request.RoleIds ?? [],
            tenantId,
            httpContext.User,
            dbContext,
            cancellationToken);
        if (roles is null)
        {
            return AuthorityExceeded();
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.TenantId == tenantId,
            cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var existingRoleIds = await dbContext.UserRoles
            .Where(userRole => userRole.UserId == user.Id)
            .Join(
                dbContext.Roles.Where(role => role.TenantId == tenantId),
                userRole => userRole.RoleId,
                role => role.Id,
                (_, role) => role.Id)
            .ToArrayAsync(cancellationToken);
        var existingRoles = await userManager.GetRolesAsync(user);
        var removeResult = await userManager.RemoveFromRolesAsync(user, existingRoles);
        if (!removeResult.Succeeded)
        {
            return IdentityFailure(removeResult.Errors);
        }

        var addResult = await userManager.AddToRolesAsync(
            user,
            roles.Select(role => role.Name!));
        if (!addResult.Succeeded)
        {
            return IdentityFailure(addResult.Errors);
        }

        await userManager.UpdateSecurityStampAsync(user);
        var now = timeProvider.GetUtcNow();
        await dbContext.RefreshSessions
            .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAtUtc, now),
                cancellationToken);

        await auditWriter.WriteAsync(
            "Authorization.RolesChanged",
            "Succeeded",
            "User",
            user.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken,
            beforeSummaryJson: AuditSummary.Serialize(new
            {
                roleIds = existingRoleIds.Order().ToArray(),
            }),
            afterSummaryJson: AuditSummary.Serialize(new
            {
                roleIds = roles.Select(role => role.Id).Order().ToArray(),
            }));
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var permissions = (request.Permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (permissions.Any(permission => !LoadIxPermissions.All.Contains(permission)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Permissions)] = ["One or more permissions are invalid."],
            });
        }

        var role = await dbContext.Roles.SingleOrDefaultAsync(
            candidate => candidate.Id == roleId && candidate.TenantId == tenantId,
            cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        var beforePermissions = role.GetPermissions().Order(StringComparer.Ordinal).ToArray();
        if (!httpContext.User.HasClaim(
                LoadIxClaimTypes.PlatformAdministrator,
                bool.TrueString)
            && beforePermissions.Concat(permissions)
                .Any(permission => !requestContext.HasPermission(permission)))
        {
            return AuthorityExceeded();
        }

        if (!beforePermissions.ToHashSet(StringComparer.Ordinal).SetEquals(permissions)
            && (string.IsNullOrWhiteSpace(request.Reason)
                || request.Reason.Trim().Length > 500))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] =
                ["A reason of no more than 500 characters is required when permissions change."],
            });
        }
        role.SetPermissions(permissions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var affectedUserIds = await dbContext.UserRoles
            .Where(userRole => userRole.RoleId == role.Id)
            .Select(userRole => userRole.UserId)
            .ToArrayAsync(cancellationToken);
        foreach (var affectedUserId in affectedUserIds)
        {
            var affectedUser = await dbContext.Users.SingleAsync(
                user => user.Id == affectedUserId && user.TenantId == tenantId,
                cancellationToken);
            await userManager.UpdateSecurityStampAsync(affectedUser);
        }

        var now = timeProvider.GetUtcNow();
        if (affectedUserIds.Length > 0)
        {
            await dbContext.RefreshSessions
                .Where(session => affectedUserIds.Contains(session.UserId)
                    && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(session => session.RevokedAtUtc, now),
                    cancellationToken);
        }

        await auditWriter.WriteAsync(
            "Authorization.PermissionsChanged",
            "Succeeded",
            "Role",
            role.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken,
            beforeSummaryJson: AuditSummary.Serialize(new { permissions = beforePermissions }),
            afterSummaryJson: AuditSummary.Serialize(new { permissions }),
            reason: request.Reason);
        return Results.NoContent();
    }

    private static async Task<IResult> DeactivateUserAsync(
        Guid userId,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        if (userId == requestContext.UserId)
        {
            return Results.BadRequest(new GenericMessageResponse(
                "You cannot deactivate your own account."));
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.TenantId == tenantId,
            cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.IsActive = false;
        user.ModifiedAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateSecurityStampAsync(user);
        await dbContext.RefreshSessions
            .Where(session =>
                session.UserId == user.Id && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    timeProvider.GetUtcNow()),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "User.Deactivated",
            "Succeeded",
            "User",
            user.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListUserSessionsAsync(
        Guid userId,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        if (!await dbContext.Users.AnyAsync(
                user => user.Id == userId && user.TenantId == tenantId,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.RefreshSessions
            .Where(session =>
                session.TenantId == tenantId
                && session.UserId == userId
                && session.RevokedAtUtc == null
                && session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => new SessionResponse(
                session.Id,
                session.CreatedAtUtc,
                session.ExpiresAtUtc,
                session.UserAgent,
                session.IpAddress,
                false))
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeUserSessionAsync(
        Guid userId,
        Guid sessionId,
        HttpContext httpContext,
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId
                && candidate.UserId == userId
                && candidate.TenantId == tenantId,
            cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.RevokedAtUtc ??= timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "User.SessionRevoked",
            "Succeeded",
            "Session",
            session.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListAuditEventsAsync(
        ICurrentRequestContext requestContext,
        LoadIxDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenant(requestContext);
        var events = await dbContext.SecurityAuditEvents
            .Where(auditEvent => auditEvent.TenantId == tenantId)
            .OrderByDescending(auditEvent => auditEvent.OccurredAtUtc)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(200)
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.EventType,
                auditEvent.Outcome,
                auditEvent.TargetType,
                auditEvent.TargetId,
                auditEvent.ActorUserId,
                auditEvent.IsSupportContext,
                auditEvent.OccurredAtUtc,
                auditEvent.CorrelationId,
                auditEvent.RequestMetadataJson,
                auditEvent.BeforeSummaryJson,
                auditEvent.AfterSummaryJson,
                auditEvent.Reason))
            .ToListAsync(cancellationToken);
        return Results.Ok(events);
    }

    private static async Task<List<ApplicationRole>?> AuthorizedRolesAsync(
        IReadOnlyCollection<Guid> roleIds,
        TenantId tenantId,
        ClaimsPrincipal principal,
        LoadIxDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return null;
        }

        var roles = await dbContext.Roles
            .Where(role => role.TenantId == tenantId && roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);
        if (roles.Count != roleIds.Distinct().Count())
        {
            return null;
        }

        if (principal.HasClaim(
                LoadIxClaimTypes.PlatformAdministrator,
                bool.TrueString))
        {
            return roles;
        }

        var actorPermissions = principal
            .FindAll(LoadIxClaimTypes.Permission)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        return roles.All(role => role.GetPermissions().IsSubsetOf(actorPermissions))
            ? roles
            : null;
    }

    private static async Task SendInvitationAsync(
        UserInvitation invitation,
        string rawToken,
        IAccountEmailSender emailSender,
        string webOrigin,
        CancellationToken cancellationToken)
    {
        var uri = QueryHelpers.AddQueryString(
            new Uri(new Uri(webOrigin), "/accept-invitation").ToString(),
            "token",
            rawToken);
        await emailSender.SendInvitationAsync(
            invitation.Email,
            invitation.DisplayName,
            new Uri(uri),
            cancellationToken);
    }

    private static TenantId RequiredTenant(ICurrentRequestContext context) =>
        context.TenantId
        ?? throw new InvalidOperationException(
            "An authenticated tenant context is required.");

    private static Guid CurrentUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private static IResult AuthorityExceeded() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The requested roles exceed your authority.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "role_authority_exceeded",
            });

    private static IResult IdentityFailure(IEnumerable<IdentityError> errors) =>
        Results.ValidationProblem(errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray()));
}

public sealed record InviteUserRequest(
    string Email,
    string DisplayName,
    IReadOnlyCollection<Guid> RoleIds);

public sealed record AssignRolesRequest(IReadOnlyCollection<Guid> RoleIds);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<string> Permissions,
    string? Reason = null);

public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool EmailConfirmed,
    IReadOnlyCollection<UserRoleSummary> Roles);

public sealed record RoleResponse(
    Guid Id,
    string Name,
    IReadOnlyCollection<string> Permissions);

public sealed record InvitationResponse(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<UserRoleSummary> Roles);

public sealed record AuditEventResponse(
    long Id,
    string EventType,
    string Outcome,
    string TargetType,
    string? TargetId,
    Guid? ActorUserId,
    bool IsSupportContext,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    string? RequestMetadataJson,
    string? BeforeSummaryJson,
    string? AfterSummaryJson,
    string? Reason);
