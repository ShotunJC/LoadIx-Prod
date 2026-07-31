using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Tenancy;
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
public sealed class ContractEndpointTests(
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
            InitialCatalog = $"LoadIx_Contracts_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:Database", _connectionString);
            host.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await dbContext.Database.MigrateAsync();
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
    public async Task CreatesPreviewsActivatesSuspendsAndAuditsContract()
    {
        var client = await AdministratorClientAsync();
        var model = ContractModel($"CTR-{Guid.NewGuid():N}"[..18]);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/contracts", model);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createdResponse.Headers.ETag.Should().NotBeNull();
        var created = (await createdResponse.Content
            .ReadFromJsonAsync<ContractDetail>())!;
        created.Status.Should().Be("Draft");

        var preview = await client.PostAsJsonAsync(
            $"/api/v1/contracts/{created.Id}/rate-preview",
            new RatePreviewModel(
                created.Rates.Single().Id,
                null,
                null,
                30m,
                DateOnly.FromDateTime(DateTime.UtcNow)));
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var calculation = (await preview.Content
            .ReadFromJsonAsync<RateCalculationResponse>())!;
        calculation.TotalAmount.Should().Be(3692.50m);
        calculation.Breakdown.Select(item => item.Code)
            .Should().Equal("base", "tonnage", "fuel");
        calculation.RateSnapshot.PerTonneAmount.Should().Be(100m);

        var activatedResponse = await client.PostAsJsonAsync(
            $"/api/v1/contracts/{created.Id}/activate",
            new ContractTransitionModel(created.Version));
        activatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activated = (await activatedResponse.Content
            .ReadFromJsonAsync<ContractDetail>())!;
        activated.Status.Should().Be("Active");

        var suspended = await client.PostAsJsonAsync(
            $"/api/v1/contracts/{created.Id}/suspend",
            new ContractTransitionModel(activated.Version));
        suspended.StatusCode.Should().Be(HttpStatusCode.OK);
        (await suspended.Content.ReadFromJsonAsync<ContractDetail>())!
            .Status.Should().Be("Suspended");

        var history = await client.GetFromJsonAsync<ContractHistoryItem[]>(
            $"/api/v1/contracts/{created.Id}/history");
        history!.Select(item => item.EventType).Should()
            .Contain(["Contract.Created", "Contract.Activated", "Contract.Suspended"]);
    }

    [Fact]
    public async Task RejectsOverlapsIncompleteActivationAndStaleVersion()
    {
        var client = await AdministratorClientAsync();
        var model = ContractModel($"CTR-{Guid.NewGuid():N}"[..18]);
        var overlapping = model with
        {
            Rates =
            [
                model.Rates[0],
                model.Rates[0] with
                {
                    Id = Guid.NewGuid(),
                    EffectiveFrom = model.Rates[0].EffectiveFrom.AddDays(1),
                },
            ],
        };
        var overlapResponse = await client.PostAsJsonAsync(
            "/api/v1/contracts", overlapping);
        overlapResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var incomplete = model with
        {
            ContractNumber = $"EMPTY-{Guid.NewGuid():N}"[..20],
            Routes = [],
            Rates = [],
            CommodityIds = [],
        };
        var incompleteResponse = await client.PostAsJsonAsync(
            "/api/v1/contracts", incomplete);
        var incompleteContract = (await incompleteResponse.Content
            .ReadFromJsonAsync<ContractDetail>())!;
        (await client.PostAsJsonAsync(
            $"/api/v1/contracts/{incompleteContract.Id}/activate",
            new ContractTransitionModel(incompleteContract.Version)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await CreateAsync(client, model);
        var update = model with { Name = "Updated contract", Version = created.Version };
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/contracts/{created.Id}", update);
        updateResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        (await client.PutAsJsonAsync(
            $"/api/v1/contracts/{created.Id}", update))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EnforcesPagingPermissionsAndTenantIsolation()
    {
        var admin = await AdministratorClientAsync();
        (await admin.GetAsync("/api/v1/contracts?pageSize=101"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var created = await CreateAsync(
            admin,
            ContractModel($"CTR-{Guid.NewGuid():N}"[..18]));
        var list = await admin.GetFromJsonAsync<ContractPagedResponse<ContractListItem>>(
            "/api/v1/contracts?status=all&sort=contractNumber&direction=asc");
        list!.Items.Should().Contain(item => item.Id == created.Id);

        var readOnly = await ReadOnlyClientAsync();
        (await readOnly.GetAsync("/api/v1/contracts"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await readOnly.PostAsJsonAsync(
            "/api/v1/contracts",
            ContractModel($"FORB-{Guid.NewGuid():N}"[..18])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var foreignId = await CreateForeignContractAsync();
        (await admin.GetAsync($"/api/v1/contracts/{foreignId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateForeignContractAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var tenantId = TenantId.New();
        dbContext.Tenants.Add(Tenant.Create(
            tenantId,
            "Contract Isolation Tenant",
            "Contract Isolation Tenant (Pty) Ltd",
            $"ISO-{tenantId.Value:N}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value);
        await dbContext.SaveChangesAsync();
        context.Set(
            tenantId,
            Guid.NewGuid(),
            permissions: [LoadIxPermissions.ContractManage]);
        var customer = Customer.Create(
            Guid.NewGuid(),
            tenantId,
            "Isolation Customer (Pty) Ltd",
            "Isolation Customer",
            $"ISO-{Guid.NewGuid():N}",
            null,
            30,
            Address.Create(
                "1 Isolation Road", null, "Test", "Test", "0001", "ZA").Value,
            null).Value;
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var id = Guid.NewGuid();
        var contract = LoadIx.Domain.Commercial.TransportContract.Create(
            id,
            tenantId,
            customer.Id,
            "ISO-CONTRACT",
            "Foreign contract",
            new DateOnly(2026, 1, 1),
            null,
            "ZAR",
            30,
            0,
            null,
            [],
            [],
            []).Value;
        dbContext.TransportContracts.Add(contract);
        await dbContext.SaveChangesAsync();
        return id;
    }

    private static async Task<ContractDetail> CreateAsync(
        HttpClient client,
        ContractWriteModel model)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contracts", model);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContractDetail>())!;
    }

    private static ContractWriteModel ContractModel(string number)
    {
        var routeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new ContractWriteModel(
            DevelopmentSeedIds.CustomerIds[0],
            number,
            "Synthetic minerals transport",
            today.AddDays(-30),
            today.AddDays(365),
            "ZAR",
            30,
            1.5m,
            "Synthetic integration contract",
            [
                new ContractRouteWriteModel(
                    routeId,
                    "JHB-MP",
                    DevelopmentSeedIds.LocationIds[0],
                    DevelopmentSeedIds.LocationIds[1]),
            ],
            [DevelopmentSeedIds.CommodityIds[0]],
            [
                new ContractRateWriteModel(
                    Guid.NewGuid(),
                    routeId,
                    "BasePlusPerTonne",
                    today.AddDays(-30),
                    today.AddDays(365),
                    null,
                    500m,
                    100m,
                    1000m,
                    5.5m),
            ]);
    }

    private async Task<HttpClient> AdministratorClientAsync() =>
        await AuthenticatedClientAsync(
            DevelopmentIdentitySeeder.AdministratorEmail,
            AdministratorPassword);

    private async Task<HttpClient> ReadOnlyClientAsync()
    {
        var email = $"contract-readonly-{Guid.NewGuid():N}@loadix.example.invalid";
        const string password = "Read Only Contract User 2026!";
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var now = TimeProvider.System.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Contract Read-only User",
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        var role = await dbContext.Roles.SingleAsync(
            item => item.DisplayName == "Read only");
        (await userManager.AddToRoleAsync(user, role.Name!)).Succeeded.Should().BeTrue();
        return await AuthenticatedClientAsync(email, password);
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
}
