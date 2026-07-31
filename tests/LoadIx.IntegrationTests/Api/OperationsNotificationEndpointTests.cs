using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Domain.Common;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Notifications;
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
public sealed class OperationsNotificationEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private readonly List<WebApplicationFactory<Program>> _factories = [];
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            database.ConnectionString)
        {
            InitialCatalog = $"LoadIx_Operations_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>()
            .SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>()
            .SeedAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        foreach (var factory in _factories) factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OperationsAndNotificationsRequireAuthentication()
    {
        var client = CreateFactory().CreateClient();

        (await client.GetAsync("/api/v1/operations/board"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/notifications"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OperationsRequiresTripViewAndTenantNotificationDataIsFiltered()
    {
        var admin = await LoginAsync(
            DevelopmentIdentitySeeder.AdministratorEmail,
            AdministratorPassword);
        (await admin.GetAsync("/api/v1/operations/board"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var noTripViewEmail = $"operations-readonly-{Guid.NewGuid():N}@loadix.example.invalid";
        await CreateUserWithoutRolesAsync(noTripViewEmail);
        var restricted = await LoginAsync(noTripViewEmail, "Operations User 2026!");
        (await restricted.GetAsync("/api/v1/operations/board"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var foreignNotificationId = Guid.NewGuid();
        var foreignTenant = TenantId.New();
        await using (var scope = CreateFactory().Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
            db.Tenants.Add(Tenant.Create(
                foreignTenant,
                "Foreign Synthetic Tenant",
                "Foreign Synthetic Tenant (Pty) Ltd",
                $"FOREIGN-{foreignTenant.Value:N}",
                null,
                "ZAR",
                "Africa/Johannesburg").Value!);
            db.NotificationMessages.Add(new NotificationMessage
            {
                Id = foreignNotificationId,
                TenantId = foreignTenant,
                RecipientUserId = DevelopmentSeedIds.ActorId,
                Type = "trip.assignment",
                Title = "Foreign synthetic notification",
                Body = "This must never cross the tenant boundary.",
                DeduplicationKey = $"foreign-{foreignNotificationId:N}",
                CreatedAtUtc = TimeProvider.System.GetUtcNow(),
            });
            await db.SaveChangesAsync();
        }

        var notifications = await admin.GetFromJsonAsync<NotificationInboxResponse>(
            "/api/v1/notifications");
        notifications!.Items.Should().NotContain(item => item.Id == foreignNotificationId);

        var templates = await admin.GetFromJsonAsync<NotificationTemplateResponse[]>(
            "/api/v1/notifications/templates");
        templates.Should().NotContain(item => item.SubjectTemplate.Contains(
            "Foreign synthetic",
            StringComparison.Ordinal));
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", _connectionString);
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
        });
        _factories.Add(factory);
        return factory;
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateFactory().CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var payload = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return client;
    }

    private async Task CreateUserWithoutRolesAsync(string email)
    {
        await using var scope = CreateFactory().Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<
            UserManager<ApplicationUser>>();
        var now = TimeProvider.System.GetUtcNow();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = DevelopmentSeedIds.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Operations Test User",
            IsActive = true,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        (await userManager.CreateAsync(user, "Operations User 2026!"))
            .Succeeded.Should().BeTrue();
    }

    private sealed record NotificationInboxResponse(
        IReadOnlyCollection<NotificationItem> Items,
        int UnreadCount);

    private sealed record NotificationItem(Guid Id);

    private sealed record NotificationTemplateResponse(
        Guid Id,
        string NotificationType,
        string Channel,
        string SubjectTemplate,
        string BodyTemplate,
        DateTimeOffset ModifiedAtUtc);
}
