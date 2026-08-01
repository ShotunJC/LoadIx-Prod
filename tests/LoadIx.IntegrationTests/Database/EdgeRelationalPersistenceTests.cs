using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Edge;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.IntegrationTests.Database;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class EdgeRelationalPersistenceTests(MsSqlDatabaseFixture database)
{
    [Fact]
    public async Task EdgeRelationshipsRejectOrphansAndProtectHistoricalRows()
    {
        var connectionString = await CreateDatabaseAsync();
        var data = await CreateEdgeDataAsync(connectionString);

        await AssertDbUpdateFailureAsync(
            connectionString,
            data.TenantId,
            db =>
            {
                db.EdgeDeviceCredentials.Add(EdgeDeviceCredential.Create(
                    Guid.NewGuid(),
                    data.TenantId,
                    Guid.NewGuid(),
                    EdgeDeviceCredentialType.Device,
                    "hashed-secret",
                    DateTimeOffset.UtcNow).Value!);
            });

        await AssertDbUpdateFailureAsync(
            connectionString,
            data.TenantId,
            db =>
            {
                db.RawWeightReadings.Add(RawWeightReading.Create(
                    Guid.NewGuid(),
                    data.TenantId,
                    data.DeviceId,
                    data.WeighbridgeDeviceId,
                    10m,
                    WeightUnit.Kilograms,
                    false,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    null,
                    "WEIGHT|value=10|unit=kg|stable=false",
                    "generic-line-v1",
                    Guid.NewGuid().ToString("N"),
                    Guid.NewGuid()).Value!);
            });

        await AssertDbUpdateFailureAsync(
            connectionString,
            data.TenantId,
            db =>
            {
                var command = EdgeCommand.Create(
                    Guid.NewGuid(),
                    data.TenantId,
                    data.DeviceId,
                    data.CredentialId,
                    "TicketSubmission",
                    $"ticket-{Guid.NewGuid():N}",
                    $"ticket-nonce-{Guid.NewGuid():N}",
                    new string('A', 64),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    null).Value!;
                db.EdgeCommands.Add(command);
                db.EdgeTicketSubmissions.Add(EdgeTicketSubmission.Create(
                    Guid.NewGuid(),
                    data.TenantId,
                    data.DeviceId,
                    Guid.NewGuid(),
                    command.Id,
                    "ORPHAN-WEIGHBRIDGE",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    "ticket",
                    "text/plain",
                    null,
                    Guid.NewGuid().ToString("N")).Value!);
            });

        await AssertDbUpdateFailureAsync(
            connectionString,
            data.TenantId,
            db =>
            {
                db.StableWeightReadings.Add(StableWeightReading.Create(
                    Guid.NewGuid(),
                    data.TenantId,
                    Guid.NewGuid(),
                    data.DeviceId,
                    data.WeighbridgeDeviceId,
                    10m,
                    WeightUnit.Kilograms,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    null,
                    Guid.NewGuid().ToString("N")).Value!);
            });

        await AddHistoricalReadingAndAuditAsync(connectionString, data);

        await AssertDbUpdateFailureAsync(
            connectionString,
            data.TenantId,
            db =>
            {
                var device = db.EdgeDevices
                    .IgnoreQueryFilters()
                    .Single(item => item.Id == data.DeviceId);
                db.EdgeDevices.Remove(device);
            });

        await using var verification = CreateContext(connectionString, data.TenantId);
        (await verification.DbContext.SecurityAuditEvents
                .IgnoreQueryFilters()
                .CountAsync(item => item.ActorDeviceId == data.DeviceId))
            .Should().Be(1);
        (await verification.DbContext.RawWeightReadings
                .IgnoreQueryFilters()
                .CountAsync(item => item.EdgeDeviceId == data.DeviceId))
            .Should().Be(1);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_EdgeRelationships_{Guid.NewGuid():N}",
        };
        var connectionString = builder.ConnectionString;
        await using var context = CreateContext(connectionString);
        await context.DbContext.Database.MigrateAsync();
        return connectionString;
    }

    private static async Task<EdgePersistenceData> CreateEdgeDataAsync(
        string connectionString)
    {
        var tenantId = TenantId.New();
        await using var tenantContext = CreateContext(connectionString);
        tenantContext.DbContext.Tenants.Add(Tenant.Create(
            tenantId,
            "Edge relationship tenant",
            "Edge relationship tenant (Pty) Ltd",
            $"EDGE-{tenantId.Value:N}",
            null,
            "ZAR",
            "Africa/Johannesburg").Value);
        await tenantContext.DbContext.SaveChangesAsync();

        await using var context = CreateContext(connectionString, tenantId);
        var location = Location.Create(
            Guid.NewGuid(),
            tenantId,
            null,
            "Edge relationship site",
            $"ERS-{tenantId.Value:N}"[..12],
            LocationType.Generic,
            Address.Create(
                "1 Edge Road",
                null,
                "Johannesburg",
                "Gauteng",
                "2000",
                "ZA").Value!,
            null,
            null,
            null,
            null).Value!;
        var device = EdgeDevice.Create(
            Guid.NewGuid(),
            tenantId,
            "Relationship test device",
            location.Id,
            "generic-line",
            "generic-line-v1").Value!;
        device.Register(DateTimeOffset.UtcNow, "relationship-test");
        var credential = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            tenantId,
            device.Id,
            EdgeDeviceCredentialType.Device,
            new PasswordHasher<EdgeDeviceCredential>()
                .HashPassword(null!, "hashed-only-test-secret"),
            DateTimeOffset.UtcNow).Value!;
        var weighbridge = WeighbridgeDevice.Create(
            Guid.NewGuid(),
            tenantId,
            device.Id,
            location.Id,
            "Relationship indicator",
            $"REL-{Guid.NewGuid():N}"[..15],
            "generic-line",
            "generic-line-v1").Value!;
        context.DbContext.AddRange(location, device, credential, weighbridge);
        await context.DbContext.SaveChangesAsync();
        return new EdgePersistenceData(
            tenantId,
            device.Id,
            credential.Id,
            weighbridge.Id);
    }

    private static async Task AddHistoricalReadingAndAuditAsync(
        string connectionString,
        EdgePersistenceData data)
    {
        await using var context = CreateContext(connectionString, data.TenantId);
        var now = DateTimeOffset.UtcNow;
        var command = EdgeCommand.Create(
            Guid.NewGuid(),
            data.TenantId,
            data.DeviceId,
            data.CredentialId,
            "WeightReading",
            $"history-{Guid.NewGuid():N}",
            $"history-nonce-{Guid.NewGuid():N}",
            new string('B', 64),
            now,
            now,
            "history").Value!;
        command.Succeed();
        var raw = RawWeightReading.Create(
            Guid.NewGuid(),
            data.TenantId,
            data.DeviceId,
            data.WeighbridgeDeviceId,
            25m,
            WeightUnit.Kilograms,
            false,
            now,
            now,
            null,
            "WEIGHT|value=25|unit=kg|stable=false",
            "generic-line-v1",
            "history-reading",
            command.Id).Value!;
        context.DbContext.Add(command);
        context.DbContext.Add(raw);
        context.DbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = data.TenantId,
            ActorDeviceId = data.DeviceId,
            EventType = "EdgeDevice.HistoricalReading",
            Outcome = "Succeeded",
            TargetType = "EdgeDevice",
            TargetId = data.DeviceId.ToString(),
            OccurredAtUtc = now,
            IpAddress = string.Empty,
        });
        await context.DbContext.SaveChangesAsync();
    }

    private static async Task AssertDbUpdateFailureAsync(
        string connectionString,
        TenantId tenantId,
        Action<LoadIxDbContext> arrange)
    {
        await using var context = CreateContext(connectionString, tenantId);
        arrange(context.DbContext);
        var act = () => context.DbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

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

    private sealed record EdgePersistenceData(
        TenantId TenantId,
        Guid DeviceId,
        Guid CredentialId,
        Guid WeighbridgeDeviceId);

    private sealed record TestContext(
        LoadIxDbContext DbContext,
        CurrentRequestContext RequestContext) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }
}
