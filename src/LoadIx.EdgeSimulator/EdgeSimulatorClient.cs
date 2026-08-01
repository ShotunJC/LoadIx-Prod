using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using LoadIx.Application.Edge;

namespace LoadIx.EdgeSimulator;

public sealed class EdgeSimulatorClient(HttpClient httpClient)
{
    private string? _deviceSecret;

    public Guid DeviceId { get; private set; }

    public void SetCredentials(Guid deviceId, string deviceSecret)
    {
        DeviceId = deviceId;
        _deviceSecret = deviceSecret;
    }

    public async Task<EdgeCredentialIssued> BootstrapAsync(
        Guid deviceId,
        string bootstrapToken,
        string? agentVersion,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/edge/bootstrap",
            new
            {
                deviceId,
                bootstrapToken,
                agentVersion,
            },
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EdgeCredentialIssued>(
                   cancellationToken: cancellationToken))
               ?? throw new InvalidOperationException("Bootstrap returned no credential.");
    }

    public async Task<EdgeConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var response = await SendAuthenticatedAsync(
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
        var response = await SendAuthenticatedAsync(
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
        var response = await SendAuthenticatedAsync(
            HttpMethod.Post,
            "/api/v1/edge/readings",
            pending.Reading,
            cancellationToken,
            pending.IdempotencyKey);
        return await IsSuccessfulAsync(response, cancellationToken);
    }

    public async Task<EdgeCredentialIssued?> RotateCredentialAsync(
        CancellationToken cancellationToken)
    {
        var response = await SendAuthenticatedAsync(
            HttpMethod.Post,
            "/api/v1/edge/credential/rotate",
            null,
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<EdgeCredentialIssued>(
            cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(_deviceSecret) || DeviceId == Guid.Empty)
            throw new InvalidOperationException("The simulator has no active credentials.");
        return await SendAsync(
            method,
            path,
            body,
            (request, timestamp, nonce) =>
            {
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Id",
                    DeviceId.ToString());
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Secret",
                    _deviceSecret);
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Timestamp",
                    timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                request.Headers.TryAddWithoutValidation(
                    "X-LoadIx-Device-Nonce",
                    nonce);
                request.Headers.TryAddWithoutValidation(
                    "Idempotency-Key",
                    idempotencyKey ?? CreateKey());
            },
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        Action<HttpRequestMessage, DateTimeOffset, string>? configureHeaders,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        var timestamp = DateTimeOffset.UtcNow;
        var nonce = CreateKey();
        configureHeaders?.Invoke(request, timestamp, nonce);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static async Task<bool> IsSuccessfulAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return true;
        if (response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.Conflict)
        {
            _ = await response.Content.ReadAsStringAsync(cancellationToken);
            return false;
        }

        return false;
    }

    private static string CreateKey() => $"sim-{Guid.NewGuid():N}";
}
