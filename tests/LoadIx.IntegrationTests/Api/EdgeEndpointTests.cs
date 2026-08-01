using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Edge;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Edge;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.Infrastructure.Tenancy;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class EdgeEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private WebApplicationFactory<Program> _factory = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_Edge_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        _factory = CreateFactory();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
            .SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>()
            .SeedAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeviceAuthenticationReplayProtectionAndOfflineReplayAreEnforced()
    {
        var testRunId = Guid.NewGuid().ToString("N");

        var admin = await AdministratorClientAsync();
        var device = await CreateDeviceAsync(admin);
        var anonymous = _factory.CreateClient();

        var bootstrap = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/edge/bootstrap");

        AddEnvelopeHeaders(
            bootstrap,
            $"bootstrap-key-{testRunId}",
            $"bootstrap-nonce-{testRunId}");

        bootstrap.Content = JsonContent.Create(new
        {
            deviceId = device.DeviceId,
            bootstrapToken = device.BootstrapToken,
            agentVersion = "integration-agent/1",
        });

        var bootstrapResponse = await anonymous.SendAsync(bootstrap);

        var bootstrapResponseBody =
            await bootstrapResponse.Content.ReadAsStringAsync();

        bootstrapResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the bootstrap response was: {bootstrapResponseBody}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        var credential = JsonSerializer.Deserialize<EdgeCredentialIssued>(
            bootstrapResponseBody,
            options);

        credential.Should().NotBeNull();
        credential!.Secret.Should().NotBeNullOrWhiteSpace();

        var invalidSecret = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/heartbeat",
            device.DeviceId,
            "not-the-secret",
            $"invalid-key-{testRunId}",
            $"invalid-nonce-{testRunId}",
            new
            {
                deviceTimestampUtc = DateTimeOffset.UtcNow,
                status = "Running",
            });

        var invalidSecretResponse = await anonymous.SendAsync(invalidSecret);

        invalidSecretResponse.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized);

        var heartbeatKey = $"heartbeat-key-{testRunId}";
        var heartbeatNonce = $"heartbeat-nonce-{testRunId}";
        var heartbeatTimestamp = DateTimeOffset.UtcNow;

        var first = await SendHeartbeatAsync(
            device.DeviceId,
            credential.Secret,
            heartbeatKey,
            heartbeatNonce,
            "Running",
            heartbeatTimestamp);

        var firstResponseBody = await first.Content.ReadAsStringAsync();

        first.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the first heartbeat response was: {firstResponseBody}");

        var firstPayload = JsonSerializer.Deserialize<EdgeHeartbeatAck>(
            firstResponseBody,
            options);

        firstPayload.Should().NotBeNull();
        firstPayload!.IsReplay.Should().BeFalse();

        var replay = await SendHeartbeatAsync(
            device.DeviceId,
            credential.Secret,
            heartbeatKey,
            heartbeatNonce,
            "Running",
            heartbeatTimestamp);

        var replayResponseBody = await replay.Content.ReadAsStringAsync();

        replay.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the heartbeat replay response was: {replayResponseBody}");

        var replayPayload = JsonSerializer.Deserialize<EdgeHeartbeatAck>(
            replayResponseBody,
            options);

        replayPayload.Should().NotBeNull();
        replayPayload!.IsReplay.Should().BeTrue();

        var changedReplay = await SendHeartbeatAsync(
            device.DeviceId,
            credential.Secret,
            heartbeatKey,
            heartbeatNonce,
            "Changed",
            heartbeatTimestamp);

        var changedReplayResponseBody =
            await changedReplay.Content.ReadAsStringAsync();

        changedReplay.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            $"the changed replay response was: {changedReplayResponseBody}");

        var changedIdempotency = await SendHeartbeatAsync(
            device.DeviceId,
            credential.Secret,
            heartbeatKey,
            $"heartbeat-changed-nonce-{testRunId}",
            "Changed",
            heartbeatTimestamp);

        var changedIdempotencyResponseBody =
            await changedIdempotency.Content.ReadAsStringAsync();

        changedIdempotency.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            $"the changed idempotency response was: " +
            $"{changedIdempotencyResponseBody}");

        var oldCapturedAt = DateTimeOffset.UtcNow.AddDays(-2);
        var readingKey = $"reading-key-{testRunId}";

        var reading = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/readings",
            device.DeviceId,
            credential.Secret,
            readingKey,
            $"reading-nonce-first-{testRunId}",
            new
            {
                weighbridgeDeviceId = device.WeighbridgeDeviceId,
                displayedValue = 12345.250m,
                unit = "kg",
                isStable = true,
                capturedAtUtc = oldCapturedAt,
                vehicleRegistration = "SIM-001",
                serialMessageMetadata =
                    "WEIGHT|value=12345.250|unit=kg|stable=true",
                parserVersion = "generic-line-v1",
                correlationId = (string?)null,
            });

        var readingResponse = await anonymous.SendAsync(reading);
        var readingResponseBody =
            await readingResponse.Content.ReadAsStringAsync();

        readingResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the first reading response was: {readingResponseBody}");

        var firstReadingReceipt =
            JsonSerializer.Deserialize<EdgeReadingReceipt>(
                readingResponseBody,
                options);

        firstReadingReceipt.Should().NotBeNull();
        firstReadingReceipt!.IsReplay.Should().BeFalse();

        var offlineReplay = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/readings",
            device.DeviceId,
            credential.Secret,
            readingKey,
            $"reading-nonce-replay-{testRunId}",
            new
            {
                weighbridgeDeviceId = device.WeighbridgeDeviceId,
                displayedValue = 12345.250m,
                unit = "kg",
                isStable = true,
                capturedAtUtc = oldCapturedAt,
                vehicleRegistration = "SIM-001",
                serialMessageMetadata =
                    "WEIGHT|value=12345.250|unit=kg|stable=true",
                parserVersion = "generic-line-v1",
                correlationId = (string?)null,
            });

        var offlineReplayResponse =
            await anonymous.SendAsync(offlineReplay);

        var offlineReplayResponseBody =
            await offlineReplayResponse.Content.ReadAsStringAsync();

        offlineReplayResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the offline replay response was: " +
            $"{offlineReplayResponseBody}");

        var offlineReplayReceipt =
            JsonSerializer.Deserialize<EdgeReadingReceipt>(
                offlineReplayResponseBody,
                options);

        offlineReplayReceipt.Should().NotBeNull();
        offlineReplayReceipt!.IsReplay.Should().BeTrue();

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<LoadIxDbContext>();

        var rawReadingCount = await db.RawWeightReadings
            .IgnoreQueryFilters()
            .CountAsync(item =>
                item.EdgeDeviceId == device.DeviceId);

        rawReadingCount.Should().Be(1);

        var stableReadingCount = await db.StableWeightReadings
            .IgnoreQueryFilters()
            .CountAsync(item =>
                item.EdgeDeviceId == device.DeviceId);

        stableReadingCount.Should().Be(1);

        var storedCredential = await db.EdgeDeviceCredentials
            .IgnoreQueryFilters()
            .SingleAsync(item =>
                item.EdgeDeviceId == device.DeviceId
                && item.Type == EdgeDeviceCredentialType.Device);

        storedCredential.SecretHash.Should().NotBe(credential.Secret);
    }
    [Fact]
    public async Task CredentialRotationInvalidatesTheOldSecretAndRevocationStopsTheDevice()
    {
        var admin = await AdministratorClientAsync();
        var device = await CreateDeviceAsync(admin);
        var bootstrap = await RegisterAsync(device);
        var rotation = await admin.PostAsync(
            $"/api/v1/edge-devices/{device.DeviceId}/credentials/rotate",
            null);
        rotation.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await rotation.Content
            .ReadFromJsonAsync<EdgeCredentialIssued>())!;

        var oldSecret = DeviceRequest(
            HttpMethod.Get,
            "/api/v1/edge/configuration",
            device.DeviceId,
            bootstrap.Secret,
            "old-secret-key-001",
            "old-secret-nonce-001");
        (await _factory.CreateClient().SendAsync(oldSecret)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        var currentSecret = DeviceRequest(
            HttpMethod.Get,
            "/api/v1/edge/configuration",
            device.DeviceId,
            rotated.Secret,
            "current-secret-key-001",
            "current-secret-nonce-001");
        (await _factory.CreateClient().SendAsync(currentSecret)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var ticketCapturedAt = DateTimeOffset.UtcNow;
        var ticket = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/tickets",
            device.DeviceId,
            rotated.Secret,
            "ticket-key-0001",
            "ticket-nonce-0001",
            new
            {
                weighbridgeDeviceId = device.WeighbridgeDeviceId,
                ticketNumber = "SIM-TICKET-001",
                capturedAtUtc = ticketCapturedAt,
                content = "simulated ticket content",
                contentType = "text/plain",
                correlationId = "sim-ticket-1",
            });
        (await _factory.CreateClient().SendAsync(ticket)).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var ticketReplay = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/tickets",
            device.DeviceId,
            rotated.Secret,
            "ticket-key-0001",
            "ticket-nonce-0002",
            new
            {
                weighbridgeDeviceId = device.WeighbridgeDeviceId,
                ticketNumber = "SIM-TICKET-001",
                capturedAtUtc = ticketCapturedAt,
                content = "simulated ticket content",
                contentType = "text/plain",
                correlationId = "sim-ticket-1",
            });
        var ticketReplayResponse = await _factory.CreateClient()
            .SendAsync(ticketReplay);
        ticketReplayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ticketReplayResponse.Content
            .ReadFromJsonAsync<EdgeTicketReceipt>())!
            .IsReplay.Should().BeTrue();

        (await admin.PostAsync(
            $"/api/v1/edge-devices/{device.DeviceId}/revoke",
            null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var revoked = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/heartbeat",
            device.DeviceId,
            rotated.Secret,
            "revoked-key-0001",
            "revoked-nonce-0001",
            new
            {
                deviceTimestampUtc = DateTimeOffset.UtcNow,
                status = "Running",
            });
        (await _factory.CreateClient().SendAsync(revoked)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaleDeviceTimestampIsRejectedBeforeTheApplicationCommand()
    {
        var admin = await AdministratorClientAsync();
        var device = await CreateDeviceAsync(admin);
        var credential = await RegisterAsync(device);
        var request = DeviceRequest(
            HttpMethod.Get,
            "/api/v1/edge/diagnostic-status",
            device.DeviceId,
            credential.Secret,
            "stale-key-0001",
            "stale-nonce-0001");
        request.Headers.Remove("X-LoadIx-Device-Timestamp");
        request.Headers.Add(
            "X-LoadIx-Device-Timestamp",
            DateTimeOffset.UtcNow.AddMinutes(-20).ToUnixTimeSeconds().ToString(
                CultureInfo.InvariantCulture));

        (await _factory.CreateClient().SendAsync(request)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EdgeDeviceRecordsAreTenantIsolated()
    {
        var foreign = await CreateForeignDeviceAsync();
        var admin = await AdministratorClientAsync();

        (await admin.GetAsync(
            $"/api/v1/edge-devices/{foreign.DeviceId}/diagnostics"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync(
            $"/api/v1/edge-devices/{foreign.DeviceId}/live-reading"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<EdgeDeviceListResponse>(
            "/api/v1/edge-devices/?pageSize=100"))!
            .Items.Should().NotContain(item => item.Id == foreign.DeviceId);
    }

    private static async Task<BootstrapResponse> CreateDeviceAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync(
            "/api/v1/edge-devices/registration-bootstrap",
            new
            {
                name = $"Simulator {Guid.NewGuid():N}",
                siteId = DevelopmentSeedIds.LocationIds[0],
                weighbridgeName = "Synthetic Indicator",
                weighbridgeCode = $"SIM-{Guid.NewGuid():N}"[..15],
                protocol = "generic-line",
                parserVersion = "generic-line-v1",
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BootstrapResponse>())!;
    }

    private async Task<EdgeCredentialIssued> RegisterAsync(BootstrapResponse device)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/edge/bootstrap");
        AddEnvelopeHeaders(request, "register-key-" + Guid.NewGuid().ToString("N")[..12],
            "register-nonce-" + Guid.NewGuid().ToString("N")[..12]);
        request.Content = JsonContent.Create(new
        {
            deviceId = device.DeviceId,
            bootstrapToken = device.BootstrapToken,
            agentVersion = "integration-agent/1",
        });
        var response = await _factory.CreateClient().SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EdgeCredentialIssued>())!;
    }

    private async Task<HttpResponseMessage> SendHeartbeatAsync(
        Guid deviceId,
        string secret,
        string idempotencyKey,
        string nonce,
        string status,
        DateTimeOffset deviceTimestampUtc)
    {
        var request = DeviceRequest(
            HttpMethod.Post,
            "/api/v1/edge/heartbeat",
            deviceId,
            secret,
            idempotencyKey,
            nonce,
            new
            {
                deviceTimestampUtc,
                status,
            });
        return await _factory.CreateClient().SendAsync(request);
    }

    private static HttpRequestMessage DeviceRequest(
        HttpMethod method,
        string path,
        Guid deviceId,
        string secret,
        string idempotencyKey,
        string nonce,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(
            "X-LoadIx-Device-Id",
            deviceId.ToString());
        request.Headers.TryAddWithoutValidation(
            "X-LoadIx-Device-Secret",
            secret);
        AddEnvelopeHeaders(request, idempotencyKey, nonce);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static void AddEnvelopeHeaders(
        HttpRequestMessage request,
        string idempotencyKey,
        string nonce)
    {
        request.Headers.TryAddWithoutValidation(
            "X-LoadIx-Device-Timestamp",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(
                CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("X-LoadIx-Device-Nonce", nonce);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private async Task<HttpClient> AdministratorClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(
                DevelopmentIdentitySeeder.AdministratorEmail,
                AdministratorPassword));
        login.EnsureSuccessStatusCode();
        var response = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", response.AccessToken);
        return client;
    }

    private async Task<ForeignDevice> CreateForeignDeviceAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantId = TenantId.New();
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        db.Tenants.Add(Tenant.Create(
            tenantId,
            "Foreign Edge Tenant",
            "Foreign Edge Tenant (Pty) Ltd",
            $"EDGE-{tenantId.Value:N}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value);
        await db.SaveChangesAsync();
        context.Set(tenantId, Guid.NewGuid(), permissions: LoadIxPermissions.All);
        var device = EdgeDevice.Create(
            Guid.NewGuid(),
            tenantId,
            "Foreign edge",
            Guid.NewGuid(),
            "generic-line",
            "generic-line-v1").Value!;
        device.Register(DateTimeOffset.UtcNow, "foreign-test");
        var secret = "ForeignSecret-Only-For-Integration";
        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<EdgeDeviceCredential>>();
        var credential = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            tenantId,
            device.Id,
            EdgeDeviceCredentialType.Device,
            hasher.HashPassword(null!, secret),
            DateTimeOffset.UtcNow).Value!;
        var weighbridge = WeighbridgeDevice.Create(
            Guid.NewGuid(),
            tenantId,
            device.Id,
            device.SiteId,
            "Foreign indicator",
            $"FOREIGN-{Guid.NewGuid():N}"[..18],
            "generic-line",
            "generic-line-v1").Value!;
        db.AddRange(device, credential, weighbridge);
        await db.SaveChangesAsync();
        return new ForeignDevice(device.Id);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", _connectionString);
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });

    private sealed record BootstrapResponse(
        Guid DeviceId,
        Guid WeighbridgeDeviceId,
        string DeviceName,
        string SiteName,
        string BootstrapToken,
        DateTimeOffset ExpiresAtUtc);

    private sealed record ForeignDevice(Guid DeviceId);

    private sealed record AuthResponse(string AccessToken);
}
