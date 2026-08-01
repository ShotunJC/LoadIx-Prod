using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using LoadIx.Application.Edge;

namespace LoadIx.EdgeSimulator;

public sealed class EdgeSimulatorClient(HttpClient httpClient)
{
    private string? deviceSecret;

    public Guid DeviceId { get; private set; }

    public void SetCredentials(Guid deviceId, string secret)
    {
        DeviceId = deviceId;
        deviceSecret = secret;
    }

    public async Task<EdgeCredentialIssued> BootstrapAsync(
        Guid deviceId,
        string bootstrapToken,
        string proposedSecret,
        string idempotencyKey,
        string nonce,
        string? agentVersion,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => SendAsync(
                HttpMethod.Post,
                "/api/v1/edge/bootstrap",
                new
                {
                    deviceId,
                    bootstrapToken,
                    deviceSecret = proposedSecret,
                    agentVersion,
                },
                (request, timestamp, requestNonce, requestKey) =>
                {
                    request.Headers.TryAddWithoutValidation(
                        "X-LoadIx-Device-Timestamp",
                        timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                    request.Headers.TryAddWithoutValidation(
                        "X-LoadIx-Device-Nonce",
                        requestNonce);
                    request.Headers.TryAddWithoutValidation(
                        "Idempotency-Key",
                        requestKey);
                },
                cancellationToken,
                idempotencyKey,
                nonce),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var issued = await response.Content.ReadFromJsonAsync<EdgeCredentialIssued>(
            cancellationToken: cancellationToken);
        return issued is null
            ? throw new InvalidOperationException("Bootstrap returned no credential.")
            : issued with { Secret = issued.Secret ?? proposedSecret };
    }

    public async Task<EdgeConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedAsync(
            HttpMethod.Get,
            "/api/v1/edge/configuration",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EdgeConfiguration>(
                   cancellationToken: cancellationToken))
               ?? throw new InvalidOperationException("Configuration returned no payload.");
    }

    public async Task<bool> SendHeartbeatAsync(
        EdgeHeartbeatModel heartbeat,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedAsync(
            HttpMethod.Post,
            "/api/v1/edge/heartbeat",
            heartbeat,
            cancellationToken);
        return await IsSuccessfulAsync(response, cancellationToken);
    }

    public async Task<bool> SendReadingAsync(
        PendingReading pending,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedAsync(
            HttpMethod.Post,
            "/api/v1/edge/readings",
            pending.Reading,
            cancellationToken,
            pending.IdempotencyKey);
        return await IsSuccessfulAsync(response, cancellationToken);
    }

    public async Task<EdgeCredentialIssued?> RotateCredentialAsync(
        string replacementSecret,
        string idempotencyKey,
        string nonce,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => SendAuthenticatedAsync(
                HttpMethod.Post,
                "/api/v1/edge/credential/rotate",
                new { replacementSecret },
                cancellationToken,
                idempotencyKey,
                nonce),
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var issued = await response.Content.ReadFromJsonAsync<EdgeCredentialIssued>(
            cancellationToken: cancellationToken);
        return issued is null
            ? null
            : issued with { Secret = issued.Secret ?? replacementSecret };
    }

    public async Task<bool> ConfirmCredentialRotationAsync(
        string idempotencyKey,
        string nonce,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => SendAuthenticatedAsync(
                HttpMethod.Post,
                "/api/v1/edge/credential/rotation-confirm",
                null,
                cancellationToken,
                idempotencyKey,
                nonce),
            cancellationToken);
        return await IsSuccessfulAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        string? idempotencyKey = null,
        string? nonce = null)
    {
        if (string.IsNullOrWhiteSpace(deviceSecret) || DeviceId == Guid.Empty)
            throw new InvalidOperationException("The simulator has no active credentials.");
        return await SendAsync(
            method,
            path,
            body,
            (request, timestamp, requestNonce, requestKey) =>
            {
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Id",
                    DeviceId.ToString());
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Secret",
                    deviceSecret);
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Timestamp",
                    timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Nonce",
                    requestNonce);
                request.Headers.TryAddWithoutValidation(
                    "Idempotency-Key",
                    requestKey);
            },
            cancellationToken,
            idempotencyKey,
            nonce);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        Action<HttpRequestMessage, DateTimeOffset, string, string>? configureHeaders,
        CancellationToken cancellationToken,
        string? idempotencyKey = null,
        string? nonce = null)
    {
        using var request = new HttpRequestMessage(method, path);
        var timestamp = DateTimeOffset.UtcNow;
        configureHeaders?.Invoke(
            request,
            timestamp,
            nonce ?? CreateKey(),
            idempotencyKey ?? CreateKey());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await send();
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    private static async Task<bool> IsSuccessfulAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return true;
        if (response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.Conflict
            or HttpStatusCode.TooManyRequests)
        {
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        return false;
    }

    private static string CreateKey() => $"sim-{Guid.NewGuid():N}";
}
