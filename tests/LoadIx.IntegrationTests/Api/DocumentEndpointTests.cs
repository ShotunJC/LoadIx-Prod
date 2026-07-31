using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Documents;
using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.Infrastructure.Tenancy;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class DocumentEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_Documents_{Guid.NewGuid():N}",
        };
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:Database", builder.ConnectionString);
            host.UseSetting("Documents:MaximumSizeMegabytes", "1");
            host.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
            host.ConfigureTestServices(services =>
            {
                services.RemoveAll<IObjectStorage>();
                var storage = new TestObjectStorage();
                services.AddSingleton(storage);
                services.AddSingleton<IObjectStorage>(storage);
            });
        });
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await context.Database.MigrateAsync();
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
    public async Task SanitizesNamesStreamsSafelyAndDeduplicatesRetries()
    {
        var client = await AdministratorClientAsync();
        var driverId = await DriverIdAsync(client);
        var pdf = "%PDF-1.7\nSynthetic document\n%%EOF"u8.ToArray();

        var first = await UploadAsync(
            client,
            driverId,
            "../../unsafe name.pdf",
            pdf,
            "application/pdf",
            "document-upload-001");
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = (await first.Content.ReadFromJsonAsync<DocumentItem>())!;
        document.FileName.Should().Be("unsafe name.pdf");
        document.Sha256Checksum.Should().HaveLength(64);

        var replay = await UploadAsync(
            client,
            driverId,
            "../../unsafe name.pdf",
            pdf,
            "application/pdf",
            "document-upload-001");
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadFromJsonAsync<DocumentItem>())!.Id
            .Should().Be(document.Id);

        var storage = _factory.Services.GetRequiredService<TestObjectStorage>();
        storage.Objects.Should().ContainSingle();
        storage.Objects.Keys.Single().Should().NotContain("unsafe");

        var download = await client.GetAsync(
            $"/api/v1/documents/{document.Id}/content");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType
            .Should().Be("application/octet-stream");
        download.Content.Headers.ContentDisposition!.DispositionType
            .Should().Be("attachment");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);

        var list = await client.GetFromJsonAsync<DocumentItem[]>(
            $"/api/v1/documents?ownerType=Driver&ownerId={driverId}");
        list.Should().ContainSingle();

        var delete = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/documents/{document.Id}");
        delete.Headers.TryAddWithoutValidation("If-Match", $"\"{document.Version}\"");
        (await client.SendAsync(delete)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/documents/{document.Id}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RejectsInvalidSignaturesExcessiveFilesAndKeyReuse()
    {
        var client = await AdministratorClientAsync();
        var driverId = await DriverIdAsync(client);
        var valid = "%PDF-1.7\nFirst\n%%EOF"u8.ToArray();
        (await UploadAsync(
            client,
            driverId,
            "first.pdf",
            valid,
            "application/pdf",
            "document-upload-reuse"))
            .EnsureSuccessStatusCode();

        var reused = await UploadAsync(
            client,
            driverId,
            "second.pdf",
            "%PDF-1.7\nSecond\n%%EOF"u8.ToArray(),
            "application/pdf",
            "document-upload-reuse");
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var executable = await UploadAsync(
            client,
            driverId,
            "malware.pdf",
            "MZ executable"u8.ToArray(),
            "application/pdf",
            "document-invalid-signature");
        executable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await executable.Content.ReadAsStringAsync())
            .Should().Contain("document.signature_invalid");

        var oversized = await UploadAsync(
            client,
            driverId,
            "oversized.pdf",
            [.. "%PDF-1.7\n"u8.ToArray(), .. new byte[1024 * 1024]],
            "application/pdf",
            "document-oversized");
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await oversized.Content.ReadAsStringAsync())
            .Should().Contain("document.size_invalid");
    }

    [Fact]
    public async Task ConcurrentIdempotentUploadsConvergeOnOneDocument()
    {
        var client = await AdministratorClientAsync();
        var driverId = await DriverIdAsync(client);
        var pdf = "%PDF-1.7\nConcurrent synthetic document\n%%EOF"u8.ToArray();

        var responses = await Task.WhenAll(
            UploadAsync(
                client,
                driverId,
                "concurrent.pdf",
                pdf,
                "application/pdf",
                "document-concurrent-retry"),
            UploadAsync(
                client,
                driverId,
                "concurrent.pdf",
                pdf,
                "application/pdf",
                "document-concurrent-retry"));

        responses.Should().OnlyContain(response =>
            response.StatusCode == HttpStatusCode.Created);
        var documents = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<DocumentItem>()));
        documents.Select(document => document!.Id).Distinct()
            .Should().ContainSingle();
        _factory.Services.GetRequiredService<TestObjectStorage>()
            .Objects.Should().ContainSingle();
    }

    [Fact]
    public async Task DeniesAnonymousAndCrossTenantDocumentAccess()
    {
        var client = await AdministratorClientAsync();
        var driverId = await DriverIdAsync(client);
        var response = await UploadAsync(
            client,
            driverId,
            "private.pdf",
            "%PDF-1.7\nPrivate\n%%EOF"u8.ToArray(),
            "application/pdf",
            "document-private");
        var document = (await response.Content.ReadFromJsonAsync<DocumentItem>())!;

        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync($"/api/v1/documents/{document.Id}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = _factory.Services.CreateAsyncScope();
        var requestContext =
            scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        requestContext.Set(
            LoadIx.Domain.Common.TenantId.New(),
            Guid.NewGuid(),
            permissions: [LoadIxPermissions.DocumentView]);
        var service = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var foreign = await service.DownloadAsync(
            document.Id,
            false,
            CancellationToken.None);
        foreign.Failure.Should().Be(DocumentFailure.NotFound);

        requestContext.Set(
            DevelopmentSeedIds.TenantId,
            Guid.NewGuid(),
            permissions: []);
        var unauthorized = await service.DownloadAsync(
            document.Id,
            false,
            CancellationToken.None);
        unauthorized.Failure.Should().Be(DocumentFailure.Forbidden);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid ownerId,
        string fileName,
        byte[] bytes,
        string contentType,
        string idempotencyKey)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Driver"), "ownerType");
        form.Add(new StringContent(ownerId.ToString()), "ownerId");
        form.Add(new StringContent("DriverDocument"), "documentType");
        form.Add(new StringContent("Synthetic test document"), "notes");
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents")
        {
            Content = form,
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<Guid> DriverIdAsync(HttpClient client)
    {
        var drivers = await client.GetFromJsonAsync<PagedResponse<DriverItem>>(
            "/api/v1/drivers?pageSize=1&status=active");
        return drivers!.Items.Single().Id;
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

    private sealed class TestObjectStorage : IObjectStorage
    {
        public ConcurrentDictionary<string, byte[]> Objects { get; } =
            new(StringComparer.Ordinal);

        public Task EnsureReadyAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task PutAsync(
            string key,
            Stream content,
            long sizeBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            Objects[key] = memory.ToArray();
        }

        public Task<Stream> OpenReadAsync(
            string key,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(
                new MemoryStream(Objects[key], writable: false));

        public Task DeleteAsync(
            string key,
            CancellationToken cancellationToken)
        {
            Objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
