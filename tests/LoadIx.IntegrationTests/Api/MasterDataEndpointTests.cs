using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Fleet;
using LoadIx.Domain.Locations;
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
public sealed class MasterDataEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private WebApplicationFactory<Program> _factory = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var lease = await database.CreateDatabaseAsync("LoadIx_MasterData");
        _connectionString = lease.ConnectionString;
        _factory = CreateFactory();
        if (lease.RequiresInitialization)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            await dbContext.Database.MigrateAsync();
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
    public async Task ListsAreBoundedFilteredAndDeterministicallySorted()
    {
        var client = await AdministratorClientAsync();

        (await client.GetAsync("/api/v1/customers?page=1&pageSize=101"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var first = await client.GetFromJsonAsync<PagedResponse<CustomerItem>>(
            "/api/v1/customers?page=1&pageSize=2&status=all&sort=legalName&direction=asc");
        var repeated = await client.GetFromJsonAsync<PagedResponse<CustomerItem>>(
            "/api/v1/customers?page=1&pageSize=2&status=all&sort=legalName&direction=asc");

        first!.Items.Should().HaveCount(2);
        first.TotalCount.Should().Be(5);
        first.Items.Select(item => item.Id).Should()
            .Equal(repeated!.Items.Select(item => item.Id));
        first.Items.Select(item => item.LegalName).Should().BeInAscendingOrder();

        var branchVehicles = await client.GetFromJsonAsync<PagedResponse<VehicleItem>>(
            $"/api/v1/vehicles?pageSize=100&status=all&branchId={DevelopmentSeedIds.JohannesburgBranchId}");
        branchVehicles!.Items.Should().NotBeEmpty()
            .And.OnlyContain(item =>
                item.BranchId == DevelopmentSeedIds.JohannesburgBranchId);
    }

    [Fact]
    public async Task ValidationUniquenessConcurrencyStatusAndAuditAreEnforced()
    {
        var client = await AdministratorClientAsync();
        var invalid = CustomerModel(
            $"Invalid {Guid.NewGuid():N}",
            $"INV-{Guid.NewGuid():N}",
            paymentTerms: 500);
        (await client.PostAsJsonAsync("/api/v1/customers", invalid))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var customer = (await client.GetFromJsonAsync<CustomerItem>(
            $"/api/v1/customers/{DevelopmentSeedIds.CustomerIds[0]}"))!;
        var update = new CustomerWriteModel(
            customer.LegalName,
            $"{customer.TradingName} Updated",
            customer.RegistrationNumber,
            customer.TaxNumber,
            customer.PaymentTermsDays,
            customer.BillingAddress,
            customer.Contacts,
            "Updated in integration test",
            customer.Version);
        var successfulUpdate = await client.PutAsJsonAsync(
            $"/api/v1/customers/{customer.Id}",
            update);
        successfulUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(
            $"/api/v1/customers/{customer.Id}",
            update)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var current = (await successfulUpdate.Content
            .ReadFromJsonAsync<CustomerItem>())!;
        var deactivate = await client.PostAsJsonAsync(
            $"/api/v1/customers/{customer.Id}/status",
            new StatusWriteModel(false, current.Version));
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await client.GetFromJsonAsync<PagedResponse<CustomerItem>>(
            $"/api/v1/customers?status=active&search={Uri.EscapeDataString(customer.RegistrationNumber)}");
        var inactive = await client.GetFromJsonAsync<PagedResponse<CustomerItem>>(
            $"/api/v1/customers?status=inactive&search={Uri.EscapeDataString(customer.RegistrationNumber)}");
        active!.Items.Should().BeEmpty();
        inactive!.Items.Should().ContainSingle();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        (await dbContext.SecurityAuditEvents.CountAsync(item =>
            item.TargetId == customer.Id.ToString()
            && item.EventType.StartsWith("MasterData."))).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UniqueFleetRegistrationsAndManagePermissionAreEnforced()
    {
        var admin = await AdministratorClientAsync();
        var existing = (await admin.GetFromJsonAsync<PagedResponse<VehicleItem>>(
            "/api/v1/vehicles?pageSize=1&status=all"))!.Items.Single();
        var duplicate = new VehicleWriteModel(
            existing.BranchId,
            existing.Registration,
            $"UNIQUE-{Guid.NewGuid():N}"[..24],
            "Test",
            "Truck",
            2026,
            8m,
            34m,
            "Tonnes",
            new DateOnly(2027, 12, 31),
            new DateOnly(2027, 12, 31),
            true);
        (await admin.PostAsJsonAsync("/api/v1/vehicles", duplicate))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var existingTrailer = (await admin.GetFromJsonAsync<PagedResponse<TrailerItem>>(
            "/api/v1/trailers?pageSize=1&status=all"))!.Items.Single();
        var duplicateTrailer = new TrailerWriteModel(
            existingTrailer.BranchId,
            existingTrailer.Registration,
            $"UNIQUE-{Guid.NewGuid():N}"[..24],
            "Test",
            "Trailer",
            2026,
            6m,
            30m,
            "Tonnes",
            new DateOnly(2027, 12, 31),
            true);
        (await admin.PostAsJsonAsync("/api/v1/trailers", duplicateTrailer))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var readOnly = await ReadOnlyClientAsync();
        (await readOnly.GetAsync("/api/v1/customers"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await readOnly.PostAsJsonAsync(
            "/api/v1/customers",
            CustomerModel(
                $"Forbidden {Guid.NewGuid():N}",
                $"FORB-{Guid.NewGuid():N}")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EveryMasterDataAggregateIsHiddenAcrossTenantBoundaries()
    {
        var foreignIds = await CreateForeignTenantMasterDataAsync();
        var client = await AdministratorClientAsync();

        foreach (var (resource, id) in foreignIds)
        {
            (await client.GetAsync($"/api/v1/{resource}/{id}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", _connectionString);
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });

    private async Task<HttpClient> AdministratorClientAsync() =>
        await AuthenticatedClientAsync(
            DevelopmentIdentitySeeder.AdministratorEmail,
            AdministratorPassword);

    private async Task<HttpClient> ReadOnlyClientAsync()
    {
        var email = $"readonly-{Guid.NewGuid():N}@loadix.example.invalid";
        const string password = "Read Only User 2026!";
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
            DisplayName = "Read-only Test User",
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
        string email, string password)
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

    private static CustomerWriteModel CustomerModel(
        string name,
        string registration,
        int paymentTerms = 30) =>
        new(
            $"{name} (Pty) Ltd",
            name,
            registration,
            null,
            paymentTerms,
            new AddressModel(
                "1 Test Road",
                null,
                "Johannesburg",
                "Gauteng",
                "2000",
                "ZA"),
            [
                new ContactModel(
                    null,
                    "Synthetic Contact",
                    "contact@loadix.example.invalid",
                    null,
                    "Operational"),
            ],
            null);

    private async Task<IReadOnlyList<(string Resource, Guid Id)>>
        CreateForeignTenantMasterDataAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var tenantId = TenantId.New();
        dbContext.Tenants.Add(Tenant.Create(
            tenantId,
            "Foreign Synthetic Tenant",
            "Foreign Synthetic Tenant (Pty) Ltd",
            $"FOREIGN-{tenantId.Value:N}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value);
        await dbContext.SaveChangesAsync();
        context.Set(tenantId, Guid.NewGuid());
        var address = Address.Create(
            "2 Isolation Road", null, "Cape Town", "Western Cape", "8000", "ZA").Value;
        var branch = Branch.Create(
            Guid.NewGuid(), tenantId, "Foreign Branch", "FOREIGN", address, null, null).Value;
        var customer = Customer.Create(
            Guid.NewGuid(),
            tenantId,
            "Foreign Customer (Pty) Ltd",
            "Foreign Customer",
            "FOREIGN-CUSTOMER",
            null,
            30,
            address).Value;
        var location = Location.Create(
            Guid.NewGuid(),
            tenantId,
            customer.Id,
            "Foreign Site",
            "FOREIGN-SITE",
            LocationType.Depot,
            address,
            null,
            null,
            null,
            null).Value;
        var commodity = Commodity.Create(
            Guid.NewGuid(), tenantId, "Foreign Commodity", "FOREIGN-COM", WeightUnit.Tonnes).Value;
        var driver = Driver.Create(
            Guid.NewGuid(),
            tenantId,
            branch.Id,
            "Synthetic",
            "Driver",
            DriverType.Employee,
            "FOREIGN-DRIVER",
            "+27000000000",
            "FOREIGN-LICENCE",
            new DateOnly(2027, 12, 31),
            "Synthetic Emergency",
            "+27000000001").Value;
        var tare = Weight.Create(8m, WeightUnit.Tonnes).Value;
        var capacity = Weight.Create(34m, WeightUnit.Tonnes).Value;
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            tenantId,
            branch.Id,
            "FOREIGN-TRUCK",
            "FOREIGN-TRK",
            "Test",
            "Truck",
            2026,
            tare,
            capacity,
            new DateOnly(2027, 12, 31),
            new DateOnly(2027, 12, 31)).Value;
        var trailer = Trailer.Create(
            Guid.NewGuid(),
            tenantId,
            branch.Id,
            "FOREIGN-TRAILER",
            "FOREIGN-TRL",
            "Test",
            "Trailer",
            2026,
            tare,
            capacity,
            new DateOnly(2027, 12, 31)).Value;
        dbContext.AddRange(
            branch, customer, location, commodity, driver, vehicle, trailer);
        await dbContext.SaveChangesAsync();
        return
        [
            ("branches", branch.Id),
            ("customers", customer.Id),
            ("locations", location.Id),
            ("commodities", commodity.Id),
            ("drivers", driver.Id),
            ("vehicles", vehicle.Id),
            ("trailers", trailer.Id),
        ];
    }

    private sealed record AuthResponse(string AccessToken);
}
