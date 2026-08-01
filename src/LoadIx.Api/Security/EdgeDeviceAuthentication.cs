using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using LoadIx.Domain.Edge;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Identity;
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
    IOptions<EdgeDeviceOptions> edgeOptions,
    SecurityAuditWriter auditWriter)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Context.Request;
        var deviceHeader = request.Headers[
            EdgeDeviceAuthenticationDefaults.DeviceIdHeader].ToString();
        if (!Guid.TryParse(deviceHeader, out var deviceId))
        {
            return AuthenticateResult.NoResult();
        }

        var secret = request.Headers[
            EdgeDeviceAuthenticationDefaults.SecretHeader].ToString();
        var nonce = request.Headers[EdgeDeviceAuthenticationDefaults.NonceHeader].ToString();
        var idempotencyKey = request.Headers[
            EdgeDeviceAuthenticationDefaults.IdempotencyHeader].ToString();
        string? envelopeFailureCode = null;
        if (string.IsNullOrWhiteSpace(secret)
            || !TryGetRequestEnvelope(
                request,
                out var timestamp,
                out envelopeFailureCode))
        {
            var failedDevice = await dbContext.EdgeDevices
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == deviceId,
                    Context.RequestAborted);
            await RecordFailureAsync(
                deviceId,
                failedDevice?.TenantId,
                null,
                envelopeFailureCode ?? "edge.authentication.headers_invalid",
                nonce,
                idempotencyKey);
            return AuthenticateResult.Fail("The device credential is invalid.");
        }

        var credentials = await dbContext.EdgeDeviceCredentials
            .IgnoreQueryFilters()
            .Where(item => item.EdgeDeviceId == deviceId
                && item.Type == EdgeDeviceCredentialType.Device)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(Context.RequestAborted);
        var device = await dbContext.EdgeDevices
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == deviceId, Context.RequestAborted);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var matchingCredential = credentials.FirstOrDefault(credential =>
            passwordHasher.VerifyHashedPassword(
                null!,
                credential.SecretHash,
                secret) != PasswordVerificationResult.Failed);
        if (device is null
            || device.Status != EdgeDeviceStatus.Active
            || matchingCredential is null
            || !matchingCredential.IsUsableAt(now))
        {
            var failureCode = device?.Status != EdgeDeviceStatus.Active
                ? "edge.authentication.device_revoked"
                : matchingCredential is not null
                    ? "edge.authentication.credential_revoked_or_expired"
                    : "edge.authentication.invalid_secret";
            await RecordFailureAsync(
                deviceId,
                device?.TenantId,
                matchingCredential?.Id,
                failureCode,
                nonce,
                idempotencyKey);
            return AuthenticateResult.Fail("The device credential is invalid.");
        }

        var claims = new List<Claim>
        {
            new(
                LoadIxClaimTypes.EdgeDeviceId,
                device.Id.ToString()),
            new(
                LoadIxClaimTypes.EdgeCredentialId,
                matchingCredential.Id.ToString()),
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
        out string? failureCode)
    {
        timestamp = default;
        failureCode = null;
        var nonce = request.Headers[EdgeDeviceAuthenticationDefaults.NonceHeader].ToString();
        var idempotencyKey = request.Headers[
            EdgeDeviceAuthenticationDefaults.IdempotencyHeader].ToString();
        if (!long.TryParse(
                request.Headers[EdgeDeviceAuthenticationDefaults.TimestampHeader],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixSeconds))
        {
            failureCode = "edge.timestamp_invalid";
            return false;
        }

        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            failureCode = "edge.timestamp_invalid";
            return false;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if ((now - timestamp).Duration()
            > TimeSpan.FromSeconds(edgeOptions.Value.TimestampToleranceSeconds))
        {
            failureCode = "edge.timestamp_outside_tolerance";
            return false;
        }

        if (nonce.Length is not >= 16 and <= 128
            || idempotencyKey.Length is not >= 8 and <= 128
            || !IsSafeHeaderValue(nonce)
            || !IsSafeHeaderValue(idempotencyKey))
        {
            failureCode = "edge.envelope_invalid";
            return false;
        }

        return true;
    }

    private async Task RecordFailureAsync(
        Guid deviceId,
        LoadIx.Domain.Common.TenantId? tenantId,
        Guid? credentialId,
        string failureCode,
        string nonce,
        string idempotencyKey)
    {
        await auditWriter.TryWriteAsync(
            "EdgeDevice.AuthenticationFailed",
            "Failed",
            "EdgeDevice",
            deviceId.ToString(),
            tenantId,
            null,
            false,
            Context.Connection.RemoteIpAddress?.ToString(),
            Context.RequestAborted,
            deviceId,
            credentialId,
            null,
            failureCode,
            Context.TraceIdentifier,
            HashHeader(nonce),
            HashHeader(idempotencyKey));
    }

    private static string HashHeader(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsSafeHeaderValue(string value) => value.All(character =>
        char.IsAsciiLetterOrDigit(character)
        || character is '.' or ':' or '_' or '-');
}
