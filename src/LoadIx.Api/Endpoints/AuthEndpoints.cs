using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Security.Claims;
using LoadIx.Api.Configuration;
using LoadIx.Api.Security;
using LoadIx.Application.Security;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoadIx.Api.Endpoints;

public static class AuthEndpoints
{
    private const string DevelopmentRefreshCookieName = "LoadIx.Refresh";
    private const string HostRefreshCookieName = "__Host-LoadIx.Refresh";

    public static RouteGroupBuilder MapAuthEndpoints(
        this RouteGroupBuilder api,
        IWebHostEnvironment environment)
    {
        var auth = api.MapGroup("/auth").WithTags("Authentication");

        auth.MapPost("/login", LoginAsync)
            .RequireRateLimiting("authentication");
        auth.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("authentication");
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .RequireRateLimiting("password-recovery");
        auth.MapPost("/reset-password", ResetPasswordAsync)
            .RequireRateLimiting("password-recovery");
        auth.MapPost("/confirm-email", ConfirmEmailAsync)
            .RequireRateLimiting("password-recovery");
        auth.MapPost("/accept-invitation", AcceptInvitationAsync)
            .RequireRateLimiting("password-recovery");
        auth.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization();
        auth.MapGet("/me", CurrentUserAsync).RequireAuthorization();
        auth.MapGet("/sessions", ListSessionsAsync).RequireAuthorization();
        auth.MapDelete("/sessions/{sessionId:guid}", RevokeSessionAsync)
            .RequireAuthorization();

        auth.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Items[nameof(IWebHostEnvironment)] = environment;
            return await next(context);
        });
        return api;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IdentityTokenService tokenService,
        SecurityAuditWriter auditWriter,
        IOptions<AuthenticationOptions> authenticationOptions,
        TimeProvider timeProvider,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            await auditWriter.WriteAsync(
                "Authentication.Failed",
                "Failed",
                "User",
                null,
                null,
                null,
                false,
                ClientIp(httpContext),
                cancellationToken,
                failureCode: "invalid_request");
            return AuthenticationFailed();
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var candidates = await dbContext.Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .OrderBy(user => user.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        var user = candidates.Count == 1 ? candidates[0] : null;
        var isLockedOut = user is not null
            && await userManager.IsLockedOutAsync(user);
        var passwordIsValid = user is not null
            && user.IsActive
            && !isLockedOut
            && await userManager.CheckPasswordAsync(user, request.Password);
        if (user is not null && !isLockedOut)
        {
            if (passwordIsValid)
            {
                await userManager.ResetAccessFailedCountAsync(user);
            }
            else
            {
                await userManager.AccessFailedAsync(user);
                isLockedOut = await userManager.IsLockedOutAsync(user);
            }
        }

        if (!passwordIsValid || user is null || !user.EmailConfirmed)
        {
            await auditWriter.WriteAsync(
                isLockedOut ? "Authentication.LockedOut" : "Authentication.Failed",
                "Failed",
                "User",
                user?.Id.ToString(),
                user?.TenantId,
                user?.Id,
                false,
                ClientIp(httpContext),
                cancellationToken);
            return AuthenticationFailed();
        }

        var session = CreateSession(
            user,
            tokenService.CreateRefreshToken(),
            httpContext,
            authenticationOptions.Value,
            timeProvider);
        dbContext.RefreshSessions.Add(session.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        var accessToken = await tokenService.IssueAccessTokenAsync(
            user,
            session.Entity.Id,
            cancellationToken);
        WriteRefreshCookie(
            httpContext,
            session.Secret.RawToken,
            session.Entity.ExpiresAtUtc,
            environment);
        await auditWriter.WriteAsync(
            "Authentication.Login",
            "Succeeded",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.Ok(ToResponse(user, accessToken, session.Entity.Id));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        IdentityTokenService tokenService,
        SecurityAuditWriter auditWriter,
        IOptions<AuthenticationOptions> authenticationOptions,
        TimeProvider timeProvider,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(
                RefreshCookieName(environment),
                out var rawToken)
            || string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticationFailed();
        }

        var tokenHash = IdentityTokenService.HashToken(rawToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var session = await dbContext.RefreshSessions
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.TokenHash == tokenHash,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (session is null)
        {
            await transaction.CommitAsync(cancellationToken);
            DeleteRefreshCookie(httpContext, environment);
            await auditWriter.WriteAsync(
                "Authentication.RefreshFailed",
                "Failed",
                "Session",
                null,
                null,
                null,
                false,
                ClientIp(httpContext),
                cancellationToken,
                failureCode: "unknown_refresh_token");
            return AuthenticationFailed();
        }

        if (session.RevokedAtUtc.HasValue)
        {
            session.ReuseDetectedAtUtc = now;
            await dbContext.RefreshSessions
                .Where(candidate =>
                    candidate.FamilyId == session.FamilyId
                    && candidate.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        candidate => candidate.RevokedAtUtc,
                        now),
                    cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            DeleteRefreshCookie(httpContext, environment);
            await auditWriter.WriteAsync(
                "Authentication.RefreshTokenReuse",
                "Blocked",
                "SessionFamily",
                session.FamilyId.ToString(),
                session.TenantId,
                session.UserId,
                false,
                ClientIp(httpContext),
                cancellationToken);
            return AuthenticationFailed();
        }

        if (!session.IsActive(now)
            || !session.User.IsActive
            || !session.User.EmailConfirmed)
        {
            session.RevokedAtUtc ??= now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            DeleteRefreshCookie(httpContext, environment);
            await auditWriter.WriteAsync(
                "Authentication.RefreshFailed",
                "Failed",
                "Session",
                session.Id.ToString(),
                session.TenantId,
                session.UserId,
                false,
                ClientIp(httpContext),
                cancellationToken,
                failureCode: "inactive_or_expired_session");
            return AuthenticationFailed();
        }

        var replacement = CreateSession(
            session.User,
            tokenService.CreateRefreshToken(),
            httpContext,
            authenticationOptions.Value,
            timeProvider,
            session.FamilyId);
        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = replacement.Entity.Id;
        dbContext.RefreshSessions.Add(replacement.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var accessToken = await tokenService.IssueAccessTokenAsync(
            session.User,
            replacement.Entity.Id,
            cancellationToken);
        WriteRefreshCookie(
            httpContext,
            replacement.Secret.RawToken,
            replacement.Entity.ExpiresAtUtc,
            environment);
        await auditWriter.WriteAsync(
            "Authentication.Refresh",
            "Succeeded",
            "Session",
            replacement.Entity.Id.ToString(),
            session.TenantId,
            session.UserId,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.Ok(ToResponse(
            session.User,
            accessToken,
            replacement.Entity.Id));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId(httpContext.User);
        var sessionId = CurrentSessionId(httpContext.User);
        var session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId && candidate.UserId == userId,
            cancellationToken);
        if (session is not null)
        {
            session.RevokedAtUtc ??= timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        DeleteRefreshCookie(httpContext, environment);
        await auditWriter.WriteAsync(
            "Authentication.Logout",
            "Succeeded",
            "Session",
            sessionId.ToString(),
            session?.TenantId,
            userId,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAccountEmailSender emailSender,
        SecurityAuditWriter auditWriter,
        IOptions<LoadIxOptions> loadIxOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            await auditWriter.WriteAsync(
                "Authentication.PasswordResetRequested",
                "Accepted",
                "User",
                null,
                null,
                null,
                false,
                ClientIp(httpContext),
                cancellationToken,
                failureCode: "invalid_request");
            return Results.Accepted(value: new GenericMessageResponse(
                "If the account is eligible, password reset instructions will be sent."));
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var users = await dbContext.Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .OrderBy(user => user.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        var user = users.Count == 1 ? users[0] : null;
        if (user is not null && user.IsActive && user.EmailConfirmed)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var uri = BuildWebUri(
                loadIxOptions.Value.WebOrigin,
                "/reset-password",
                ("email", user.Email!),
                ("token", token));
            await emailSender.SendPasswordResetAsync(
                user.Email!,
                uri,
                cancellationToken);
        }

        await auditWriter.WriteAsync(
            "Authentication.PasswordResetRequested",
            "Accepted",
            "User",
            user?.Id.ToString(),
            user?.TenantId,
            null,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.Accepted(value: new GenericMessageResponse(
            "If the account is eligible, password reset instructions will be sent."));
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return InvalidToken();
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var users = await dbContext.Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .OrderBy(user => user.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        var user = users.Count == 1 ? users[0] : null;
        if (user is null || !user.IsActive)
        {
            return InvalidToken();
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationFailure(result.Errors);
        }

        await RevokeAllSessionsAsync(
            dbContext,
            user.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await auditWriter.WriteAsync(
            "Authentication.PasswordReset",
            "Succeeded",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Token))
        {
            return InvalidToken();
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var users = await dbContext.Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .OrderBy(user => user.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        var user = users.Count == 1 ? users[0] : null;
        if (user is null)
        {
            return InvalidToken();
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return InvalidToken();
        }

        await auditWriter.WriteAsync(
            "Authentication.EmailConfirmed",
            "Succeeded",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return InvalidToken();
        }

        var tokenHash = IdentityTokenService.HashToken(request.Token);
        var now = timeProvider.GetUtcNow();
        var invitation = await dbContext.UserInvitations.SingleOrDefaultAsync(
            candidate => candidate.TokenHash == tokenHash,
            cancellationToken);
        if (invitation is null
            || invitation.AcceptedAtUtc.HasValue
            || invitation.RevokedAtUtc.HasValue
            || invitation.ExpiresAtUtc <= now)
        {
            return InvalidToken();
        }

        var existingUser = await dbContext.Users.AnyAsync(
            user => user.TenantId == invitation.TenantId
                && user.NormalizedEmail == invitation.NormalizedEmail,
            cancellationToken);
        if (existingUser)
        {
            return InvalidToken();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = invitation.TenantId,
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true,
            DisplayName = invitation.DisplayName,
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationFailure(result.Errors);
        }

        var roleIds = ParseRoleIds(invitation.RoleIds);
        var roleNames = await dbContext.Roles
            .Where(role =>
                role.TenantId == invitation.TenantId && roleIds.Contains(role.Id))
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
        result = await userManager.AddToRolesAsync(user, roleNames);
        if (!result.Succeeded)
        {
            return ValidationFailure(result.Errors);
        }

        invitation.AcceptedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "User.InvitationAccepted",
            "Succeeded",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        LoadIxDbContext dbContext,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return AuthenticationFailed();
        }

        var user = await userManager.FindByIdAsync(
            CurrentUserId(httpContext.User).ToString());
        if (user is null || !user.IsActive)
        {
            return AuthenticationFailed();
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!result.Succeeded)
        {
            return ValidationFailure(result.Errors);
        }

        await RevokeOtherSessionsAsync(
            dbContext,
            user.Id,
            CurrentSessionId(httpContext.User),
            timeProvider.GetUtcNow(),
            cancellationToken);
        await auditWriter.WriteAsync(
            "Authentication.PasswordChanged",
            "Succeeded",
            "User",
            user.Id.ToString(),
            user.TenantId,
            user.Id,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CurrentUserAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        LoadIxDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(
            CurrentUserId(httpContext.User).ToString());
        if (user is null || !user.IsActive)
        {
            return AuthenticationFailed();
        }

        var roleIds = httpContext.User.FindAll(LoadIxClaimTypes.RoleId)
            .Select(claim => Guid.Parse(claim.Value))
            .ToArray();
        var roles = await dbContext.Roles
            .Where(role => roleIds.Contains(role.Id)
                && user.TenantId.HasValue
                && role.TenantId == user.TenantId.Value)
            .Select(role => new UserRoleSummary(role.Id, role.DisplayName))
            .ToListAsync(cancellationToken);
        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.TenantId?.Value,
            user.Email!,
            user.DisplayName,
            httpContext.User.FindAll(LoadIxClaimTypes.Permission)
                .Select(claim => claim.Value)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            roles,
            user.IsPlatformAdministrator,
            httpContext.Response.Headers.ContainsKey(
                TenantContextMiddleware.SupportContextHeader)));
    }

    private static async Task<IResult> ListSessionsAsync(
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId(httpContext.User);
        var currentSessionId = CurrentSessionId(httpContext.User);
        var now = timeProvider.GetUtcNow();
        var sessions = await dbContext.RefreshSessions
            .Where(session =>
                session.UserId == userId
                && session.RevokedAtUtc == null
                && session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => new SessionResponse(
                session.Id,
                session.CreatedAtUtc,
                session.ExpiresAtUtc,
                session.UserAgent,
                session.IpAddress,
                session.Id == currentSessionId))
            .ToListAsync(cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        LoadIxDbContext dbContext,
        SecurityAuditWriter auditWriter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId(httpContext.User);
        var session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == sessionId && candidate.UserId == userId,
            cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.RevokedAtUtc ??= timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "Authentication.SessionRevoked",
            "Succeeded",
            "Session",
            session.Id.ToString(),
            session.TenantId,
            userId,
            false,
            ClientIp(httpContext),
            cancellationToken);
        return Results.NoContent();
    }

    private static SessionSecret CreateSession(
        ApplicationUser user,
        TokenSecret secret,
        HttpContext httpContext,
        AuthenticationOptions options,
        TimeProvider timeProvider,
        Guid? familyId = null)
    {
        var now = timeProvider.GetUtcNow();
        return new SessionSecret(
            new RefreshSession
            {
                Id = Guid.NewGuid(),
                FamilyId = familyId ?? Guid.NewGuid(),
                UserId = user.Id,
                TenantId = user.TenantId,
                TokenHash = secret.Hash,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(options.RefreshTokenDays),
                UserAgent = httpContext.Request.Headers.UserAgent.ToString()[..Math.Min(
                    httpContext.Request.Headers.UserAgent.ToString().Length,
                    300)],
                IpAddress = ClientIp(httpContext),
            },
            secret);
    }

    private static AuthResponse ToResponse(
        ApplicationUser user,
        IssuedAccessToken token,
        Guid sessionId) =>
        new(
            token.AccessToken,
            token.ExpiresAtUtc,
            sessionId,
            new CurrentUserResponse(
                user.Id,
                user.TenantId?.Value,
                user.Email!,
                user.DisplayName,
                token.Permissions,
                token.Roles,
                user.IsPlatformAdministrator,
                false));

    private static void WriteRefreshCookie(
        HttpContext context,
        string token,
        DateTimeOffset expiresAt,
        IWebHostEnvironment environment) =>
        context.Response.Cookies.Append(
            RefreshCookieName(environment),
            token,
            CookieOptions(context, environment, expiresAt));

    private static void DeleteRefreshCookie(
        HttpContext context,
        IWebHostEnvironment environment) =>
        context.Response.Cookies.Delete(
            RefreshCookieName(environment),
            CookieOptions(context, environment, DateTimeOffset.UnixEpoch));

    private static CookieOptions CookieOptions(
        HttpContext context,
        IWebHostEnvironment environment,
        DateTimeOffset expiresAt) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        };

    private static string RefreshCookieName(IWebHostEnvironment environment) =>
        environment.IsDevelopment()
            ? DevelopmentRefreshCookieName
            : HostRefreshCookieName;

    private static Guid CurrentUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static Guid CurrentSessionId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(LoadIxClaimTypes.SessionId)!);

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    private static IResult AuthenticationFailed() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication failed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "authentication_failed",
            });

    private static IResult InvalidToken() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The token is invalid or expired.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "invalid_or_expired_token",
            });

    private static IResult ValidationFailure(
        IEnumerable<IdentityError> errors) =>
        Results.ValidationProblem(errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal));

    private static Uri BuildWebUri(
        string webOrigin,
        string path,
        params (string Key, string Value)[] parameters)
    {
        var baseUri = new Uri(new Uri(webOrigin), path);
        var query = parameters.Aggregate(
            baseUri.ToString(),
            (current, parameter) =>
                QueryHelpers.AddQueryString(current, parameter.Key, parameter.Value));
        return new Uri(query);
    }

    private static Guid[] ParseRoleIds(string roleIds) =>
        roleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .ToArray();

    private static Task<int> RevokeAllSessionsAsync(
        LoadIxDbContext dbContext,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions
            .Where(session =>
                session.UserId == userId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    now),
                cancellationToken);

    private static Task<int> RevokeOtherSessionsAsync(
        LoadIxDbContext dbContext,
        Guid userId,
        Guid currentSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions
            .Where(session =>
                session.UserId == userId
                && session.Id != currentSessionId
                && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.RevokedAtUtc,
                    now),
                cancellationToken);

    private sealed record SessionSecret(
        RefreshSession Entity,
        TokenSecret Secret);
}

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

public sealed record ConfirmEmailRequest(string Email, string Token);

public sealed record AcceptInvitationRequest(string Token, string Password);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid SessionId,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(
    Guid Id,
    Guid? TenantId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserRoleSummary> Roles,
    bool IsPlatformAdministrator,
    bool IsSupportContext);

public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string UserAgent,
    string IpAddress,
    bool IsCurrent);

public sealed record GenericMessageResponse(string Message);
