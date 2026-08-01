using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LoadIx.Domain.Edge;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoadIx.Api.Security;

public static class EdgeDeviceAuthenticationDefaults
{
    public const string Scheme = "EdgeDevice";
    public const string Policy = "EdgeDevice";
    public const string DeviceIdHeader = "X-LoadIx-Device-Id";
    public const string SecretHeader = "X-LoadIx-Device-Secret";
    public const string TimestampHeader = "X-LoadIx-Device-Timestamp";
    public const string NonceHeader = "X-LoadIx-Device-Nonce";
    public const string IdempotencyHeader = "Idempotency-Key";
}

public sealed class EdgeDeviceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    LoadIxDbContext dbContext,
    IPasswordHasher<EdgeDeviceCredential> passwordHasher,
    TimeProvider timeProvider,
    IOptions<EdgeDeviceOptions> edgeOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Context.Request;
        if (!Guid.TryParse(
                request.Headers[EdgeDeviceAuthenticationDefaults.DeviceIdHeader].ToString(),
                out var deviceId)
            || string.IsNullOrWhiteSpace(
                request.Headers[EdgeDeviceAuthenticationDefaults.SecretHeader])
            || !TryGetRequestEnvelope(request, out var timestamp, out var nonce, out var idempotencyKey))
        {
            return AuthenticateResult.NoResult();
        }

        var credential = await dbContext.EdgeDeviceCredentials
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.EdgeDeviceId == deviceId
                && item.Type == EdgeDeviceCredentialType.Device
                && item.RevokedAtUtc == null, Context.RequestAborted);
        var device = await dbContext.EdgeDevices
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == deviceId, Context.RequestAborted);
        if (credential is null
            || device is null
            || device.Status != EdgeDeviceStatus.Active
            || !credential.IsUsableAt(timeProvider.GetUtcNow())
            || passwordHasher.VerifyHashedPassword(
                null!,
                credential.SecretHash,
                request.Headers[EdgeDeviceAuthenticationDefaults.SecretHeader].ToString())
                == PasswordVerificationResult.Failed)
        {
            return AuthenticateResult.Fail("The device credential is invalid.");
        }

        var claims = new List<Claim>
        {
            new(
                LoadIxClaimTypes.EdgeDeviceId,
                device.Id.ToString()),
            new(
                LoadIxClaimTypes.EdgeCredentialId,
                credential.Id.ToString()),
            new(
                LoadIxClaimTypes.TenantId,
                device.TenantId.Value.ToString()),
            new(LoadIxClaimTypes.EdgeNonce, nonce),
            new(
                LoadIxClaimTypes.EdgeTimestamp,
                timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        };
        var identity = new ClaimsIdentity(claims, EdgeDeviceAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(
            principal,
            EdgeDeviceAuthenticationDefaults.Scheme));
    }

    private bool TryGetRequestEnvelope(
        HttpRequest request,
        out DateTimeOffset timestamp,
        out string nonce,
        out string idempotencyKey)
    {
        timestamp = default;
        nonce = request.Headers[EdgeDeviceAuthenticationDefaults.NonceHeader].ToString();
        idempotencyKey = request.Headers[EdgeDeviceAuthenticationDefaults.IdempotencyHeader]
            .ToString();
        if (!long.TryParse(
                request.Headers[EdgeDeviceAuthenticationDefaults.TimestampHeader],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixSeconds))
        {
            return false;
        }

        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        return (now - timestamp).Duration()
                <= TimeSpan.FromSeconds(edgeOptions.Value.TimestampToleranceSeconds)
            && nonce.Length is >= 16 and <= 128
            && idempotencyKey.Length is >= 8 and <= 128
            && IsSafeHeaderValue(nonce)
            && IsSafeHeaderValue(idempotencyKey);
    }

    private static bool IsSafeHeaderValue(string value) => value.All(character =>
        char.IsAsciiLetterOrDigit(character)
        || character is '.' or ':' or '_' or '-');
}
