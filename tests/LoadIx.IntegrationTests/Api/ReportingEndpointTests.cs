using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class ReportingEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private const string UnprivilegedPassword = "Reporting Test User 2026!";
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_Reporting_{Guid.NewGuid():N}",
        };
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:Database", builder.ConnectionString);
            host.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
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
    public async Task ReportingEndpointsRequireReportViewPermission()
    {
        var client = await UnprivilegedClientAsync();

        (await client.GetAsync("/api/v1/reports/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReversedDateRangeIsRejectedBeforeReportQuery()
    {
        var client = await AdministratorClientAsync();

        var response = await client.GetAsync(
            "/api/v1/reports/dashboard?from=2026-08-02&to=2026-08-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("report.date_range_invalid");
    }

    [Fact]
    public async Task DashboardResponseContainsAllRequiredKpisAndTenantContext()
    {
        var client = await AdministratorClientAsync();

        var response = await client.GetAsync("/api/v1/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        foreach (var property in new[]
        {
            "activeTrips", "completedTripsToday", "tonnesDeliveredToday",
            "awaitingReconciliation", "tripsOutsideTolerance", "delayedTrips",
            "fleetUtilisationPercent", "revenue", "estimatedGrossMargin",
            "customerConcentrationPercent", "upcomingLicenceExpiries",
            "generatedAtUtc", "timeZone", "currencyCode",
        })
        {
            root.TryGetProperty(property, out _).Should().BeTrue(property);
        }

        root.GetProperty("timeZone").GetString()
            .Should().Be("Africa/Johannesburg");
    }

    private async Task<HttpClient> AdministratorClientAsync() =>
        await AuthenticatedClientAsync(
            DevelopmentIdentitySeeder.AdministratorEmail,
            AdministratorPassword);

    private async Task<HttpClient> UnprivilegedClientAsync()
    {
        var email = $"reporting-no-permission-{Guid.NewGuid():N}@loadix.example.invalid";
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Reporting permission test user",
            IsActive = true,
            CreatedAtUtc = TimeProvider.System.GetUtcNow(),
            ModifiedAtUtc = TimeProvider.System.GetUtcNow(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, UnprivilegedPassword))
            .Succeeded.Should().BeTrue();
        return await AuthenticatedClientAsync(email, UnprivilegedPassword);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(
        string email,
        string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var response = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", response.AccessToken);
        return client;
    }

    private sealed record AuthResponse(string AccessToken);
}
