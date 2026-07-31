using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.IntegrationTests.Database;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class TenantPersistenceTests(MsSqlDatabaseFixture database)
{
    [Fact]
    public async Task TenantQueryFilterOnlyReturnsCurrentTenantRows()
    {
        var connectionString = await CreateDatabaseAsync();
        var firstTenantId = TenantId.New();
        var secondTenantId = TenantId.New();

        await AddTenantAndCustomerAsync(
            connectionString,
            firstTenantId,
            "First",
            "REG-FIRST");
        await AddTenantAndCustomerAsync(
            connectionString,
            secondTenantId,
            "Second",
            "REG-SECOND");

        var firstContext = CreateContext(connectionString, firstTenantId);
        await using (firstContext.DbContext)
        {
            var visibleCustomers = await firstContext.DbContext.Customers.ToListAsync();

            visibleCustomers.Should().ContainSingle();
            visibleCustomers[0].TenantId.Should().Be(firstTenantId);
        }

        var anonymousContext = CreateContext(connectionString);
        await using (anonymousContext.DbContext)
        {
            (await anonymousContext.DbContext.Customers.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task CustomerRegistrationIsUniqueWithinATenant()
    {
        var connectionString = await CreateDatabaseAsync();
        var tenantId = TenantId.New();
        var context = CreateContext(connectionString, tenantId);

        await using (context.DbContext)
        {
            context.DbContext.Tenants.Add(CreateTenant(tenantId, "Unique"));
            context.DbContext.Customers.Add(
                CreateCustomer(tenantId, "One", "SAME-REGISTRATION"));
            context.DbContext.Customers.Add(
                CreateCustomer(tenantId, "Two", "SAME-REGISTRATION"));

            var act = () => context.DbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task CustomerRegistrationCanBeReusedByDifferentTenants()
    {
        var connectionString = await CreateDatabaseAsync();

        await AddTenantAndCustomerAsync(
            connectionString,
            TenantId.New(),
            "First Scope",
            "SHARED-REGISTRATION");
        await AddTenantAndCustomerAsync(
            connectionString,
            TenantId.New(),
            "Second Scope",
            "SHARED-REGISTRATION");

        var context = CreateContext(connectionString);
        await using (context.DbContext)
        {
            (await context.DbContext.Customers
                    .IgnoreQueryFilters()
                    .CountAsync(customer =>
                        customer.RegistrationNumber == "SHARED-REGISTRATION"))
                .Should().Be(2);
        }
    }

    [Fact]
    public async Task StaleRowVersionDetectsConcurrentUpdates()
    {
        var connectionString = await CreateDatabaseAsync();
        var tenantId = TenantId.New();
        await AddTenantAndCustomerAsync(
            connectionString,
            tenantId,
            "Concurrent",
            "CONCURRENT-001");

        var first = CreateContext(connectionString, tenantId);
        var second = CreateContext(connectionString, tenantId);
        await using (first.DbContext)
        await using (second.DbContext)
        {
            var firstCustomer = await first.DbContext.Customers.SingleAsync();
            var secondCustomer = await second.DbContext.Customers.SingleAsync();

            firstCustomer.Deactivate();
            await first.DbContext.SaveChangesAsync();

            secondCustomer.Deactivate();
            var act = () => second.DbContext.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
    }

    [Fact]
    public async Task DevelopmentSeedIsRepeatableWithoutDuplicates()
    {
        var connectionString = await CreateDatabaseAsync();

        await SeedAsync(connectionString);
        await SeedAsync(connectionString);

        var context = CreateContext(connectionString);
        await using (context.DbContext)
        {
            (await context.DbContext.Tenants.CountAsync()).Should().Be(1);
            (await context.DbContext.Branches.IgnoreQueryFilters().CountAsync())
                .Should().Be(2);
            (await context.DbContext.Customers.IgnoreQueryFilters().CountAsync())
                .Should().Be(3);
            (await context.DbContext.Locations.IgnoreQueryFilters().CountAsync())
                .Should().Be(6);
            (await context.DbContext.Commodities.IgnoreQueryFilters().CountAsync())
                .Should().Be(4);
            (await context.DbContext.Drivers.IgnoreQueryFilters().CountAsync())
                .Should().Be(10);
            (await context.DbContext.Vehicles.IgnoreQueryFilters().CountAsync())
                .Should().Be(10);
            (await context.DbContext.Trailers.IgnoreQueryFilters().CountAsync())
                .Should().Be(15);
        }
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_{Guid.NewGuid():N}",
        };
        var connectionString = builder.ConnectionString;
        var context = CreateContext(connectionString);
        await using (context.DbContext)
        {
            await context.DbContext.Database.MigrateAsync();
        }

        return connectionString;
    }

    private static async Task AddTenantAndCustomerAsync(
        string connectionString,
        TenantId tenantId,
        string discriminator,
        string registration)
    {
        var context = CreateContext(connectionString, tenantId);
        await using (context.DbContext)
        {
            context.DbContext.Tenants.Add(CreateTenant(tenantId, discriminator));
            context.DbContext.Customers.Add(
                CreateCustomer(tenantId, discriminator, registration));
            await context.DbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedAsync(string connectionString)
    {
        var context = CreateContext(connectionString);
        await using (context.DbContext)
        {
            var seeder = new DevelopmentDataSeeder(
                context.DbContext,
                context.RequestContext);
            await seeder.SeedAsync(CancellationToken.None);
        }
    }

    private static Tenant CreateTenant(TenantId tenantId, string discriminator) =>
        Tenant.Create(
            tenantId,
            $"{discriminator} Tenant",
            $"{discriminator} Tenant (Pty) Ltd",
            $"TENANT-{discriminator}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value;

    private static Customer CreateCustomer(
        TenantId tenantId,
        string discriminator,
        string registration) =>
        Customer.Create(
            Guid.NewGuid(),
            tenantId,
            $"{discriminator} Customer (Pty) Ltd",
            $"{discriminator} Customer",
            registration,
            null,
            30,
            Address.Create(
                "1 Test Road",
                null,
                "Test City",
                "Test Region",
                "0001",
                "ZA").Value).Value;

    private static TestContext CreateContext(
        string connectionString,
        TenantId? tenantId = null)
    {
        var requestContext = new CurrentRequestContext();
        if (tenantId.HasValue)
        {
            requestContext.Set(tenantId.Value, Guid.NewGuid());
        }

        var options = new DbContextOptionsBuilder<LoadIxDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TestContext(
            new LoadIxDbContext(options, requestContext, TimeProvider.System),
            requestContext);
    }

    private sealed record TestContext(
        LoadIxDbContext DbContext,
        CurrentRequestContext RequestContext);
}
