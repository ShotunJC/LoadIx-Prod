using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.Instructions;
using LoadIx.Application.MasterData;
using LoadIx.Application.Notifications;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
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
public sealed class TripEndpointTests(
    MsSqlDatabaseFixture database) : IAsyncLifetime
{
    private const string AdministratorPassword = "LoadIx Demo Admin 2026!";
    private const string DriverPassword = "LoadIx Demo Driver 2026!";
    private WebApplicationFactory<Program> _factory = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var lease = await database.CreateDatabaseAsync("LoadIx_Trips");
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
    public async Task RejectsInvalidDirectTransitionThenAssignsAndDispatchesIdempotently()
    {
        var client = await AdministratorClientAsync();
        var trips = await GenerateTripsAsync(client, 1);
        var secondInstructionTrips = await GenerateTripsAsync(client, 1);
        (await client.GetAsync(
            $"/api/v1/trips/{secondInstructionTrips[0].Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var trip = await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{trips[0].Id}");
        trip.Should().NotBeNull();

        var invalidDispatch = Request(
            $"/api/v1/trips/{trip!.Id}/dispatch",
            new TripCommandModel(trip.Version),
            "direct-invalid-dispatch-001");
        (await client.SendAsync(invalidDispatch)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var resources = await ResourcesAsync(client);
        var thirdTrailerAttempt = Request(
            $"/api/v1/trips/{trip.Id}/assign",
            new AssignTripModel(
                trip.Version,
                resources.DriverIds[0],
                resources.VehicleIds[0],
                [resources.TrailerIds[0], resources.TrailerIds[1], Guid.NewGuid()]),
            "assign-trip-third-trailer-001");
        var thirdTrailerResponse = await client.SendAsync(thirdTrailerAttempt);
        thirdTrailerResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await thirdTrailerResponse.Content.ReadAsStringAsync())
            .Should().Contain("trip.trailers_invalid");

        var assignment = new AssignTripModel(
            trip.Version,
            resources.DriverIds[0],
            resources.VehicleIds[0],
            [resources.TrailerIds[0]]);
        var assign = Request(
            $"/api/v1/trips/{trip.Id}/assign",
            assignment,
            "assign-trip-integration-001");
        var assignedResponse = await client.SendAsync(assign);
        assignedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await assignedResponse.Content.ReadAsStringAsync());
        var assigned = (await assignedResponse.Content
            .ReadFromJsonAsync<TripDetail>())!;
        assigned.Status.Should().Be("Assigned");
        assigned.Assignments.Should().ContainSingle();

        var replay = Request(
            $"/api/v1/trips/{trip.Id}/assign",
            assignment,
            "assign-trip-integration-001");
        (await client.SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.OK);
        var mismatch = Request(
            $"/api/v1/trips/{trip.Id}/assign",
            assignment with { VehicleId = resources.VehicleIds[1] },
            "assign-trip-integration-001");
        (await client.SendAsync(mismatch)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        var dispatch = Request(
            $"/api/v1/trips/{trip.Id}/dispatch",
            new TripCommandModel(assigned.Version),
            "dispatch-trip-integration-001");
        var dispatchedResponse = await client.SendAsync(dispatch);
        dispatchedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await dispatchedResponse.Content.ReadAsStringAsync());
        var dispatched = (await dispatchedResponse.Content
            .ReadFromJsonAsync<TripDetail>())!;
        dispatched.Status.Should().Be("Dispatched");
        dispatched.Transitions.Select(item => item.NewStatus)
            .Should().Equal("Assigned", "Dispatched");

        var dispatchReplay = Request(
            $"/api/v1/trips/{trip.Id}/dispatch",
            new TripCommandModel(assigned.Version),
            "dispatch-trip-integration-001");
        var replayed = await client.SendAsync(dispatchReplay);
        replayed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replayed.Content.ReadFromJsonAsync<TripDetail>())!
            .Transitions.Should().HaveCount(2);
    }

    [Fact]
    public async Task DetectsResourceOverlapConcurrencyAndTenantIsolation()
    {
        var client = await AdministratorClientAsync();
        var generated = await GenerateTripsAsync(client, 2);
        var resources = await ResourcesAsync(client);
        var first = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;
        var second = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[1].Id}"))!;

        var firstAssign = Request(
            $"/api/v1/trips/{first.Id}/assign",
            new AssignTripModel(
                first.Version,
                resources.DriverIds[0],
                resources.VehicleIds[0],
                [resources.TrailerIds[0]]),
            "overlap-first-assignment-001");
        (await client.SendAsync(firstAssign)).EnsureSuccessStatusCode();

        var conflicting = Request(
            $"/api/v1/trips/{second.Id}/assign",
            new AssignTripModel(
                second.Version,
                resources.DriverIds[0],
                resources.VehicleIds[1],
                [resources.TrailerIds[1]]),
            "overlap-second-assignment-001");
        var conflict = await client.SendAsync(conflicting);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync())
            .Should().Contain("trip.resource_conflict");

        var note = await client.SendAsync(Request(
            $"/api/v1/trips/{second.Id}/notes",
            new TripNoteModel(second.Version, "Synthetic controller note"),
            "overlap-note-001"));
        note.EnsureSuccessStatusCode();
        var stale = Request(
            $"/api/v1/trips/{second.Id}/assign",
            new AssignTripModel(
                second.Version,
                resources.DriverIds[1],
                resources.VehicleIds[1],
                [resources.TrailerIds[1]]),
            "stale-assignment-version-001");
        (await client.SendAsync(stale)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        context.Set(
            LoadIx.Domain.Common.TenantId.New(),
            Guid.NewGuid(),
            permissions: [LoadIxPermissions.TripView]);
        var service = scope.ServiceProvider.GetRequiredService<ITripService>();
        var foreign = await service.GetAsync(first.Id, CancellationToken.None);
        foreign.Failure.Should().Be(TripFailure.NotFound);
    }

    [Fact]
    public async Task DriverCanOnlyReadAndProgressTheirAssignedTrip()
    {
        var administrator = await AdministratorClientAsync();
        var generated = await GenerateTripsAsync(administrator, 2);
        var resources = await ResourcesAsync(administrator);
        var assigned = (await administrator.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;
        var assignedResponse = await administrator.SendAsync(Request(
            $"/api/v1/trips/{assigned.Id}/assign",
            new AssignTripModel(
                assigned.Version,
                DevelopmentSeedIds.DriverId,
                resources.VehicleIds[0],
                [resources.TrailerIds[0]]),
            "driver-assignment-001"));
        var dispatched = await administrator.SendAsync(Request(
            $"/api/v1/trips/{assigned.Id}/dispatch",
            new TripCommandModel(
                (await assignedResponse.Content.ReadFromJsonAsync<TripDetail>())!
                .Version),
            "driver-dispatch-001"));
        dispatched.EnsureSuccessStatusCode();
        var trip = (await dispatched.Content.ReadFromJsonAsync<TripDetail>())!;

        await using (var notificationScope = _factory.Services.CreateAsyncScope())
        {
            var db = notificationScope.ServiceProvider
                .GetRequiredService<LoadIxDbContext>();
            var context = notificationScope.ServiceProvider
                .GetRequiredService<CurrentRequestContext>();
            context.Set(
                DevelopmentSeedIds.TenantId,
                Guid.NewGuid(),
                permissions: [LoadIxPermissions.AuditView]);
            var driverUserId = await db.Users
                .Where(user => user.Email == DevelopmentIdentitySeeder.DriverEmail)
                .Select(user => user.Id)
                .SingleAsync();
            var messages = await db.NotificationMessages
                .Where(message => message.RecipientUserId == driverUserId)
                .ToListAsync();
            messages.Select(message => message.Type)
                .Should().Contain(NotificationTypes.TripAssignment)
                .And.Contain(NotificationTypes.Dispatch);
            (await db.NotificationDeliveries
                    .Where(delivery => messages.Select(message => message.Id)
                        .Contains(delivery.NotificationId))
                    .CountAsync())
                .Should().BeGreaterThan(0);
        }

        var driver = await DriverClientAsync();
        var visible = await driver.GetFromJsonAsync<TripPagedResponse<TripListItem>>(
            "/api/v1/trips?page=1&pageSize=15&status=all&sort=collection&direction=asc");
        visible!.Items.Select(item => item.Id).Should().ContainSingle()
            .Which.Should().Be(trip.Id);
        (await driver.GetAsync($"/api/v1/trips/{generated[1].Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var arrival = await driver.SendAsync(Request(
            $"/api/v1/trips/{trip.Id}/collection-arrival",
            new TripCommandModel(trip.Version),
            "driver-arrival-001"));
        arrival.StatusCode.Should().Be(HttpStatusCode.OK);
        (await arrival.Content.ReadFromJsonAsync<TripDetail>())!.Status
            .Should().Be("AtCollection");

        var inbox = await driver.GetFromJsonAsync<NotificationInboxResponse>(
            "/api/v1/notifications");
        inbox!.Items.Select(item => item.Type)
            .Should().Contain(NotificationTypes.TripAssignment);
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
            $"LI-TRIP-{Guid.NewGuid():N}"[..20],
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
            "PO-TRIP-SYNTHETIC",
            "CI-TRIP-SYNTHETIC",
            "High",
            "Synthetic trip integration instruction");
        var create = await client.PostAsJsonAsync(
            "/api/v1/load-instructions", instructionModel);
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
            $"generate-operational-{Guid.NewGuid():N}");
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
            $"TC-{Guid.NewGuid():N}"[..18],
            "Trip integration contract",
            today.AddDays(-30),
            today.AddDays(365),
            "ZAR",
            30,
            1m,
            null,
            [
                new ContractRouteWriteModel(
                    routeId,
                    "TRIP-ROUTE",
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

    private async Task<HttpClient> DriverClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(
                DevelopmentIdentitySeeder.DriverEmail,
                DriverPassword));
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

    private sealed record NotificationInboxResponse(
        IReadOnlyCollection<NotificationItemResponse> Items,
        int UnreadCount);

    private sealed record NotificationItemResponse(
        Guid Id,
        string Type,
        string Title,
        string Body,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ReadAtUtc);
}
