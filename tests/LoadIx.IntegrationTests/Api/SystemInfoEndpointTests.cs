using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Contracts.System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoadIx.IntegrationTests.Api;

public sealed class SystemInfoEndpointTests :
    IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemInfoEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty))
            .CreateClient();
    }

    [Fact]
    public async Task GetSystemInfoReturnsSafePublicMetadata()
    {
        var response = await _client.GetAsync(
            "/api/v1/system/info",
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<SystemInfoResponse>(
            CancellationToken.None);
        payload.Should().NotBeNull();
        payload!.ServiceName.Should().Be("LoadIx.Api");
        payload.ApiVersion.Should().Be("v1");
        payload.Version.Should().NotBeNullOrWhiteSpace();

        var rawPayload = await response.Content.ReadAsStringAsync(
            CancellationToken.None);
        rawPayload.Should().NotContain("ConnectionStrings");
        rawPayload.Should().NotContain("Password");
        rawPayload.Should().NotContain("Environment");
    }

    [Fact]
    public async Task UnknownApiRouteReturnsProblemDetails()
    {
        var response = await _client.GetAsync(
            "/api/v1/unknown-resource",
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/problem+json");
    }
}
