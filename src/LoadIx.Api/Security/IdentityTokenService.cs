using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LoadIx.Api.Configuration;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LoadIx.Api.Security;

public sealed record IssuedAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserRoleSummary> Roles);

public sealed record UserRoleSummary(Guid Id, string Name);

public sealed class IdentityTokenService(
    UserManager<ApplicationUser> userManager,
    LoadIxDbContext dbContext,
    IOptions<AuthenticationOptions> options,
    TimeProvider timeProvider)
{
    private readonly AuthenticationOptions _options = options.Value;

    public async Task<IssuedAccessToken> IssueAccessTokenAsync(
        ApplicationUser user,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        var roles = await dbContext.Roles
            .Where(role => roleNames.Contains(role.Name!))
            .Select(role => new
            {
                role.Id,
                role.DisplayName,
                role.Permissions,
            })
            .ToListAsync(cancellationToken);
        var permissions = roles
            .SelectMany(role => role.Permissions.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(LoadIxClaimTypes.SessionId, sessionId.ToString()),
            new(LoadIxClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(
                LoadIxClaimTypes.PlatformAdministrator,
                user.IsPlatformAdministrator.ToString(CultureInfo.InvariantCulture)),
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim(
                LoadIxClaimTypes.TenantId,
                user.TenantId.Value.Value.ToString()));
        }

        claims.AddRange(roles.Select(role =>
            new Claim(LoadIxClaimTypes.RoleId, role.Id.ToString())));
        claims.AddRange(permissions.Select(permission =>
            new Claim(LoadIxClaimTypes.Permission, permission)));

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            permissions,
            roles.Select(role =>
                new UserRoleSummary(role.Id, role.DisplayName)).ToArray());
    }

    public TokenSecret CreateRefreshToken() => CreateSecret(32);

    public TokenSecret CreateInvitationToken() => CreateSecret(32);

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static TokenSecret CreateSecret(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        var rawToken = Microsoft.AspNetCore.WebUtilities.WebEncoders
            .Base64UrlEncode(bytes);
        return new TokenSecret(rawToken, HashToken(rawToken));
    }
}

public sealed record TokenSecret(string RawToken, string Hash);
