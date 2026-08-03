using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.Finance;
using LoadIx.Application.Instructions;
using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.Infrastructure.Tenancy;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class FinanceEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private WebApplicationFactory<Program> _factory = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var lease = await database.CreateDatabaseAsync("LoadIx_Finance");
        _connectionString = lease.ConnectionString;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:Database", _connectionString);
            host.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });
        if (lease.RequiresInitialization)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            await context.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
                .SeedAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>()
                .SeedAsync(CancellationToken.None);
            await database.CaptureTemplateAsync(_connectionString);
        }
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await MsSqlDatabaseFixture.DeleteDatabaseAsync(_connectionString);
    }

    [Fact]
    public async Task PreparesApprovesBatchesAndPreventsDuplicateInvoicing()
    {
        var client = await AdministratorClientAsync();
        var tax = await client.PutAsJsonAsync(
            "/api/v1/finance/tax-configuration",
            new TenantTaxConfigurationWriteModel("StandardRated", 15m));
        tax.StatusCode.Should().Be(HttpStatusCode.OK);

        var trip = await CompletedTripAsync(client);
        var financialsResponse = await client.PostAsync(
            $"/api/v1/finance/trips/{trip.Id}/recalculate",
            null);
        financialsResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await financialsResponse.Content.ReadAsStringAsync());
        var financials = (await financialsResponse.Content
            .ReadFromJsonAsync<TripFinancialsDetail>())!;
        financials.TaxExclusiveAmount.Should().Be(1250m);
        financials.TaxAmount.Should().Be(187.5m);
        financials.Total.Should().Be(1437.5m);
        financials.Profitability.ExpectedRevenue.Amount.Should().Be(1250m);

        var cost = await client.PostAsJsonAsync(
            $"/api/v1/finance/trips/{trip.Id}/costs",
            new CostEntryWriteModel(
                "FuelActual",
                250.125m,
                "Synthetic finance integration fuel slip",
                Version: financials.Version));
        cost.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await cost.Content.ReadAsStringAsync());
        financials = (await cost.Content.ReadFromJsonAsync<TripFinancialsDetail>())!;
        financials.Profitability.TotalCost.Amount.Should().Be(250.125m);
        financials.Profitability.GrossMargin.Amount.Should().Be(999.875m);

        var approval = await client.PostAsJsonAsync(
            $"/api/v1/finance/trips/{trip.Id}/approve",
            new ApproveFinancialsModel(financials.Version, "Ready to invoice"));
        approval.EnsureSuccessStatusCode();
        financials = (await approval.Content.ReadFromJsonAsync<TripFinancialsDetail>())!;
        financials.ApprovalStatus.Should().Be("Approved");

        var ready = await client
            .GetFromJsonAsync<FinancePagedResponse<InvoiceReadyTrip>>(
                "/api/v1/finance/invoice-ready?page=1&pageSize=20&sort=completed&direction=asc");
        ready!.Items.Select(item => item.FinancialsId)
            .Should().Contain(financials.Id);

        var preview = await client.PostAsJsonAsync(
            "/api/v1/finance/invoice-batches/preview",
            new InvoiceBatchPreviewRequest([financials.Id]));
        preview.EnsureSuccessStatusCode();
        (await preview.Content.ReadFromJsonAsync<InvoiceBatchPreview[]>())!
            .Should().ContainSingle()
            .Which.Total.Should().Be(1437.5m);

        var create = await client.PostAsJsonAsync(
            "/api/v1/finance/invoice-batches",
            new InvoiceBatchCreateModel([financials.Id]));
        create.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await create.Content.ReadAsStringAsync());
        var batch = (await create.Content.ReadFromJsonAsync<InvoiceBatchDetail>())!;
        batch.Lines.Should().ContainSingle();

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/finance/invoice-batches",
            new InvoiceBatchCreateModel([financials.Id]));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var exportedCsv = await client.GetAsync(
            $"/api/v1/finance/invoice-batches/{batch.Id}/export.csv");
        exportedCsv.StatusCode.Should().Be(HttpStatusCode.OK);
        (await exportedCsv.Content.ReadAsStringAsync())
            .Should().Contain(batch.BatchNumber).And.Contain(trip.TripNumber);

        var approved = await client.PostAsJsonAsync(
            $"/api/v1/finance/invoice-batches/{batch.Id}/approve",
            new VersionedFinanceCommand(batch.Version));
        approved.EnsureSuccessStatusCode();
        batch = (await approved.Content.ReadFromJsonAsync<InvoiceBatchDetail>())!;
        batch.Status.Should().Be("Approved");

        var exported = await client.PostAsJsonAsync(
            $"/api/v1/finance/invoice-batches/{batch.Id}/mark-exported",
            new VersionedFinanceCommand(batch.Version));
        exported.EnsureSuccessStatusCode();
        (await exported.Content.ReadFromJsonAsync<InvoiceBatchDetail>())!
            .Status.Should().Be("Exported");
    }

    [Fact]
    public async Task EnforcesFinancePermissionAndTenantIsolation()
    {
        var client = await AdministratorClientAsync();
        (await client.GetAsync("/api/v1/finance/tax-configuration")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync("/api/v1/finance/tax-configuration"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        context.Set(
            TenantId.New(),
            Guid.NewGuid(),
            permissions: [LoadIxPermissions.FinanceView]);
        var service = scope.ServiceProvider.GetRequiredService<IFinanceService>();
        var foreign = await service.GetTaxConfigurationAsync(CancellationToken.None);
        foreign.Value!.IsReviewed.Should().BeFalse();
        foreign.Value.Id.Should().BeNull();
    }

    private static async Task<TripDetail> CompletedTripAsync(HttpClient client)
    {
        var generated = await GenerateTripsAsync(client, 1);
        var resources = await ResourcesAsync(client);
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "assign",
            new AssignTripModel(
                trip.Version,
                resources.DriverIds[0],
                resources.VehicleIds[0],
                [resources.TrailerIds[0]]),
            "finance-assign");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "dispatch",
            new TripCommandModel(trip.Version),
            "finance-dispatch");
        foreach (var route in new[]
                 {
                     "collection-arrival",
                     "start-loading",
                 })
        {
            trip = await SendTripCommandAsync(
                client,
                trip.Id,
                route,
                new TripCommandModel(trip.Version),
                $"finance-{route}");
        }

        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "source-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42000m, 12000m, "SRC-WB-001"),
            "finance-source-wb");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "complete-collection",
            new TripCommandModel(trip.Version),
            "finance-complete-collection");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "depart-collection",
            new TripCommandModel(trip.Version),
            "finance-depart");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "delivery-arrival",
            new TripCommandModel(trip.Version),
            "finance-delivery-arrival");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "start-unloading",
            new TripCommandModel(trip.Version),
            "finance-start-unloading");
        trip = await SendTripCommandAsync(
            client,
            trip.Id,
            "destination-weighbridge",
            new TripWeighbridgeModel(trip.Version, 41000m, 12000m, "DST-WB-001"),
            "finance-destination-wb");

        foreach (var route in new[]
                 {
                     "complete-delivery",
                     "submit-reconciliation",
                     "confirm-reconciliation",
                     "confirm-required-documents",
                     "complete",
                 })
        {
            trip = await SendTripCommandAsync(
                client,
                trip.Id,
                route,
                new TripCommandModel(trip.Version),
                $"finance-{route}");
        }

        trip.Status.Should().Be("Completed");
        trip.IsReconciled.Should().BeTrue();
        trip.HasRequiredDocuments.Should().BeTrue();
        return trip;
    }

    private static async Task<TripDetail> SendTripCommandAsync<T>(
        HttpClient client,
        Guid tripId,
        string route,
        T model,
        string idempotencyKey)
    {
        var response = await client.SendAsync(Request(
            $"/api/v1/trips/{tripId}/{route}",
            model,
            idempotencyKey));
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
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

    private static async Task<GeneratedTripModel[]> GenerateTripsAsync(
        HttpClient client,
        int count)
    {
        var contract = await ActiveContractAsync(client);
        var collection = DateTimeOffset.UtcNow.AddDays(1);
        var instructionModel = new InstructionWriteModel(
            $"LI-FIN-{Guid.NewGuid():N}"[..20],
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
            "PO-FINANCE-SYNTHETIC",
            "CI-FINANCE-SYNTHETIC",
            "High",
            "Synthetic finance integration instruction");
        var create = await client.PostAsJsonAsync(
            "/api/v1/load-instructions",
            instructionModel);
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<InstructionDetail>())!;
        var plan = await client.PostAsJsonAsync(
            $"/api/v1/load-instructions/{draft.Id}/plan",
            new InstructionTransitionModel(draft.Version));
        plan.EnsureSuccessStatusCode();
        var planned = (await plan.Content.ReadFromJsonAsync<InstructionDetail>())!;
        var generate = Request(
            $"/api/v1/load-instructions/{draft.Id}/generate-trips",
            new GenerateTripsModel(planned.Version, count),
            $"generate-finance-{Guid.NewGuid():N}");
        var response = await client.SendAsync(generate);
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
            $"FC-{Guid.NewGuid():N}"[..18],
            "Finance integration contract",
            today.AddDays(-30),
            today.AddDays(365),
            "ZAR",
            30,
            1m,
            null,
            [
                new ContractRouteWriteModel(
                    routeId,
                    "FIN-ROUTE",
                    DevelopmentSeedIds.LocationIds[0],
                    DevelopmentSeedIds.LocationIds[1]),
            ],
            [DevelopmentSeedIds.CommodityIds[0]],
            [
                new ContractRateWriteModel(
                    Guid.NewGuid(),
                    routeId,
                    "FixedPerTrip",
                    today.AddDays(-30),
                    today.AddDays(365),
                    1250m,
                    null,
                    null,
                    null,
                    null),
            ]);
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

    private sealed record ResourceIds(
        IReadOnlyList<Guid> DriverIds,
        IReadOnlyList<Guid> VehicleIds,
        IReadOnlyList<Guid> TrailerIds);
}
