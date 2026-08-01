using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.Instructions;
using LoadIx.Application.MasterData;
using LoadIx.Application.Reporting;
using LoadIx.Application.Trips;
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
        root.GetProperty("activeTrips").GetInt32().Should().Be(0);
        root.GetProperty("revenue").GetDecimal().Should().Be(0m);
        root.GetProperty("estimatedGrossMargin").GetDecimal().Should().Be(0m);
        root.GetProperty("customerConcentrationPercent").GetDecimal().Should().Be(0m);
    }



    [Fact]
    public async Task CompletionReportsExcludeIncompleteTripsWithWeights()
    {
        var client = await AdministratorClientAsync();
        var generated = await GenerateTripsAsync(client, 1);
        var resources = await ResourcesAsync(client);
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;

        trip = await SendTripCommandAsync(client, trip.Id, "assign",
            new AssignTripModel(trip.Version, resources.DriverIds[0],
                resources.VehicleIds[0], [resources.TrailerIds[0]]), "report-assign");
        trip = await SendTripCommandAsync(client, trip.Id, "dispatch",
            new TripCommandModel(trip.Version), "report-dispatch");
        trip = await SendTripCommandAsync(client, trip.Id, "collection-arrival",
            new TripCommandModel(trip.Version), "report-arrival");
        trip = await SendTripCommandAsync(client, trip.Id, "start-loading",
            new TripCommandModel(trip.Version), "report-loading");
        await SendTripCommandAsync(client, trip.Id, "source-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42_000m, 12_000m, "REPORT-SRC"),
            "report-source-weight");

        var tonnage = await client.GetFromJsonAsync<ReportingPagedResponse<TonnageReportRow>>(
            "/api/v1/reports/tonnage-by-customer?page=1&pageSize=10");
        tonnage!.TotalCount.Should().Be(0);

        var dashboard = await client.GetFromJsonAsync<DashboardKpis>(
            "/api/v1/reports/dashboard");
        dashboard!.Revenue.Should().Be(0m);
        dashboard.EstimatedGrossMargin.Should().Be(0m);
    }

    private async Task<HttpClient> AdministratorClientAsync() =>
        await AuthenticatedClientAsync(
            DevelopmentIdentitySeeder.AdministratorEmail,
            AdministratorPassword);

    private static async Task<GeneratedTripModel[]> GenerateTripsAsync(
        HttpClient client,
        int count)
    {
        var contract = await ActiveContractAsync(client);
        var collection = DateTimeOffset.UtcNow.AddDays(1);
        var instructionModel = new InstructionWriteModel(
            $"LI-REPORT-{Guid.NewGuid():N}"[..20],
            contract.CustomerId,
            contract.Id,
            contract.Routes.Single().Id,
            contract.Commodities.Single().CommodityId,
            30m,
            "Tonnes",
            count,
            collection,
            collection.AddHours(2),
            collection.AddHours(4),
            collection.AddHours(8),
            "PO-REPORT",
            "CI-REPORT",
            "High",
            "Reporting integration trip");
        var create = await client.PostAsJsonAsync(
            "/api/v1/load-instructions", instructionModel);
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<InstructionDetail>())!;
        var plan = await client.PostAsJsonAsync(
            $"/api/v1/load-instructions/{draft.Id}/plan",
            new InstructionTransitionModel(draft.Version));
        plan.EnsureSuccessStatusCode();
        var planned = (await plan.Content.ReadFromJsonAsync<InstructionDetail>())!;
        var response = await client.SendAsync(Request(
            $"/api/v1/load-instructions/{draft.Id}/generate-trips",
            new GenerateTripsModel(planned.Version, count),
            $"report-generate-{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstructionDetail>())!
            .Trips.ToArray();
    }

    private static async Task<ContractDetail> ActiveContractAsync(HttpClient client)
    {
        var routeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var model = new ContractWriteModel(
            DevelopmentSeedIds.CustomerIds[0],
            $"TC-{Guid.NewGuid():N}"[..18],
            "Reporting integration contract",
            today.AddDays(-30),
            today.AddDays(365),
            "ZAR",
            30,
            1m,
            null,
            [new ContractRouteWriteModel(
                routeId,
                "REPORT-ROUTE",
                DevelopmentSeedIds.LocationIds[0],
                DevelopmentSeedIds.LocationIds[1])],
            [DevelopmentSeedIds.CommodityIds[0]],
            [new ContractRateWriteModel(
                Guid.NewGuid(), routeId, "FixedPerTrip",
                today.AddDays(-30), today.AddDays(365),
                1250m, null, null, null, null)]);
        var response = await client.PostAsJsonAsync("/api/v1/contracts", model);
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<ContractDetail>())!;
        var activation = await client.PostAsJsonAsync(
            $"/api/v1/contracts/{draft.Id}/activate",
            new ContractTransitionModel(draft.Version));
        activation.EnsureSuccessStatusCode();
        return (await activation.Content.ReadFromJsonAsync<ContractDetail>())!;
    }

    private static async Task<ResourceIds> ResourcesAsync(HttpClient client)
    {
        var drivers = await client.GetFromJsonAsync<PagedResponse<DriverItem>>(
            "/api/v1/drivers?pageSize=10&status=active");
        var vehicles = await client.GetFromJsonAsync<PagedResponse<VehicleItem>>(
            "/api/v1/vehicles?pageSize=10&status=active");
        var trailers = await client.GetFromJsonAsync<PagedResponse<TrailerItem>>(
            "/api/v1/trailers?pageSize=10&status=active");
        return new ResourceIds(
            drivers!.Items.Select(item => item.Id).ToArray(),
            vehicles!.Items.Select(item => item.Id).ToArray(),
            trailers!.Items.Select(item => item.Id).ToArray());
    }

    private static async Task<TripDetail> SendTripCommandAsync<T>(
        HttpClient client,
        Guid tripId,
        string route,
        T model,
        string idempotencyKey)
    {
        var response = await client.SendAsync(Request(
            $"/api/v1/trips/{tripId}/{route}", model, idempotencyKey));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDetail>())!;
    }

    private static HttpRequestMessage Request<T>(
        string uri,
        T model,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(model),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

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

    private sealed record ResourceIds(
        IReadOnlyList<Guid> DriverIds,
        IReadOnlyList<Guid> VehicleIds,
        IReadOnlyList<Guid> TrailerIds);
}
