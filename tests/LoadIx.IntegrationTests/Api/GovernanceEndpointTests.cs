using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Governance;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using LoadIx.IntegrationTests.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoadIx.IntegrationTests.Api;

[Collection(MsSqlDatabaseGroup.Name)]
public sealed class GovernanceEndpointTests(
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
            InitialCatalog = $"LoadIx_Governance_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        _factory = CreateFactory();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await dbContext.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
            .SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>()
            .SeedAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await MsSqlDatabaseFixture.DeleteDatabaseAsync(_connectionString);
    }

    [Fact]
    public async Task TenantExportIsSafeAndRetentionUsesOptimisticConcurrency()
    {
        var client = await AdministratorClientAsync();
        using var export = await client.PostAsync(
            "/api/v1/governance/tenant-export",
            content: null);

        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json");
        using var exportDocument = JsonDocument.Parse(
            await export.Content.ReadAsByteArrayAsync());
        var root = exportDocument.RootElement;
        root.GetProperty("users").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("excluded").GetProperty("passwords")
            .GetBoolean().Should().BeTrue();
        root.GetProperty("excluded").GetProperty("accessTokens")
            .GetBoolean().Should().BeTrue();
        root.GetProperty("excluded").GetProperty("documentBodies")
            .GetBoolean().Should().BeTrue();
        root.ToString().Should().NotContain("TokenHash");
        root.ToString().Should().NotContain("PasswordHash");

        var invalidHold = await client.PutAsJsonAsync(
            "/api/v1/governance/retention-policy",
            new RetentionPolicyUpdateModel(365, 2555, 365, true, null));
        invalidHold.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var firstUpdate = await client.PutAsJsonAsync(
            "/api/v1/governance/retention-policy",
            new RetentionPolicyUpdateModel(
                365,
                2555,
                365,
                true,
                "Pilot legal hold"));
        firstUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPolicy = (await firstUpdate.Content
            .ReadFromJsonAsync<RetentionPolicyModel>())!;
        firstPolicy.Version.Should().NotBeNullOrWhiteSpace();

        var secondUpdate = await client.PutAsJsonAsync(
            "/api/v1/governance/retention-policy",
            new RetentionPolicyUpdateModel(
                730,
                2555,
                730,
                true,
                "Updated pilot legal hold",
                firstPolicy.Version));
        secondUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

        var staleUpdate = await client.PutAsJsonAsync(
            "/api/v1/governance/retention-policy",
            new RetentionPolicyUpdateModel(
                365,
                2555,
                365,
                true,
                "Stale update",
                firstPolicy.Version));
        staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnonymisationRequiresDeactivationAndRemovesDirectIdentifiers()
    {
        var userId = await CreateTenantUserAsync();
        var client = await AdministratorClientAsync();

        var requestResponse = await client.PostAsJsonAsync(
            "/api/v1/governance/anonymisation-requests",
            new AnonymisationRequestModel(userId, "Approved integration test request"));
        requestResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await requestResponse.Content
            .ReadFromJsonAsync<AnonymisationRequestResponse>())!;

        var activeApproval = await client.PostAsJsonAsync(
            $"/api/v1/governance/anonymisation-requests/{request.Id}/approve",
            new AnonymisationApprovalModel("User remains active"));
        activeApproval.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.PostAsync($"/api/v1/users/{userId}/deactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approval = await client.PostAsJsonAsync(
            $"/api/v1/governance/anonymisation-requests/{request.Id}/approve",
            new AnonymisationApprovalModel("Deactivated before approval"));
        approval.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = (await approval.Content
            .ReadFromJsonAsync<AnonymisationRequestResponse>())!;
        completed.Status.Should().Be("Completed");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == userId);
        user.Email.Should().Be($"anonymized-{userId:N}@invalid.loadix");
        user.DisplayName.Should().Be("Anonymised user");
        user.PhoneNumber.Should().BeNull();
        user.IsActive.Should().BeFalse();
        (await dbContext.SecurityAuditEvents.AnyAsync(auditEvent =>
            auditEvent.EventType == "Governance.UserAnonymised"
            && auditEvent.TargetId == userId.ToString()
            && auditEvent.Reason == "Deactivated before approval"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task AuditEventsCannotBeUpdatedOrDeleted()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var auditEvent = new SecurityAuditEvent
        {
            EventType = "Test.Audit",
            Outcome = "Succeeded",
            TargetType = "Test",
            TargetId = Guid.NewGuid().ToString(),
            OccurredAtUtc = TimeProvider.System.GetUtcNow(),
            IpAddress = "127.0.0.1",
        };
        dbContext.SecurityAuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync();

        auditEvent.Outcome = "Tampered";
        Func<Task> trackedUpdate = () => dbContext.SaveChangesAsync();
        await trackedUpdate.Should().ThrowAsync<InvalidOperationException>();

        dbContext.ChangeTracker.Clear();
        var persisted = await dbContext.SecurityAuditEvents
            .SingleAsync(item => item.Id == auditEvent.Id);
        dbContext.SecurityAuditEvents.Remove(persisted);
        Func<Task> trackedDelete = () => dbContext.SaveChangesAsync();
        await trackedDelete.Should().ThrowAsync<InvalidOperationException>();

        dbContext.ChangeTracker.Clear();
        const string tamperedOutcome = "Tampered";
        Func<Task> databaseUpdate = () => dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"UPDATE [audit].[SecurityAuditEvents] SET [Outcome] = {tamperedOutcome} WHERE [Id] = {auditEvent.Id}");
        await databaseUpdate.Should().ThrowAsync<SqlException>();
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", _connectionString);
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });

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

    private async Task<Guid> CreateTenantUserAsync()
    {
        var email = $"anonymise-{Guid.NewGuid():N}@loadix.example.invalid";
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
            DisplayName = "Anonymisation Test User",
            PhoneNumber = "+27820000000",
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, "Anonymisation User 2026!"))
            .Succeeded.Should().BeTrue();
        var role = await dbContext.Roles.SingleAsync(
            candidate => candidate.DisplayName == "Read only");
        (await userManager.AddToRoleAsync(user, role.Name!))
            .Succeeded.Should().BeTrue();
        return user.Id;
    }

    private sealed record AuthResponse(string AccessToken);
}
