using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.Instructions;
using LoadIx.Application.Security;
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
public sealed class LoadInstructionEndpointTests(
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
            InitialCatalog = $"LoadIx_Instructions_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:Database", builder.ConnectionString);
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

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await MsSqlDatabaseFixture.DeleteDatabaseAsync(_connectionString);
    }

    [Fact]
    public async Task CreatesPlansAndGeneratesTripsIdempotently()
    {
        var client = await AdministratorClientAsync();
        var contract = await ActiveContractAsync(client);
        var model = InstructionModel(contract);
        var createdResponse = await client.PostAsJsonAsync(
            "/api/v1/load-instructions", model);
        createdResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await createdResponse.Content.ReadAsStringAsync());
        var created = (await createdResponse.Content
            .ReadFromJsonAsync<InstructionDetail>())!;
        created.Status.Should().Be("Draft");
        created.CreatedByUserId.Should().NotBeNull();

        var plannedResponse = await client.PostAsJsonAsync(
            $"/api/v1/load-instructions/{created.Id}/plan",
            new InstructionTransitionModel(created.Version));
        plannedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var planned = (await plannedResponse.Content
            .ReadFromJsonAsync<InstructionDetail>())!;
        planned.Status.Should().Be("Planned");

        const string idempotencyKey = "integration-trip-generation-001";
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/load-instructions/{created.Id}/generate-trips")
        {
            Content = JsonContent.Create(new GenerateTripsModel(planned.Version, 2)),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var generatedResponse = await client.SendAsync(request);
        generatedResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            await generatedResponse.Content.ReadAsStringAsync());
        var generated = (await generatedResponse.Content
            .ReadFromJsonAsync<InstructionDetail>())!;
        generated.Trips.Should().HaveCount(2);
        generated.Trips.Sum(item => item.PlannedQuantity).Should().Be(30m);

        var replay = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/load-instructions/{created.Id}/generate-trips")
        {
            Content = JsonContent.Create(new GenerateTripsModel(planned.Version, 2)),
        };
        replay.Headers.Add("Idempotency-Key", idempotencyKey);
        var replayed = await client.SendAsync(replay);
        replayed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replayed.Content.ReadFromJsonAsync<InstructionDetail>())!
            .Trips.Should().HaveCount(2);

        var mismatch = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/load-instructions/{created.Id}/generate-trips")
        {
            Content = JsonContent.Create(new GenerateTripsModel(planned.Version, 3)),
        };
        mismatch.Headers.Add("Idempotency-Key", idempotencyKey);
        (await client.SendAsync(mismatch)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        var allowedUpdateResponse = await client.PutAsJsonAsync(
            $"/api/v1/load-instructions/{created.Id}",
            model with
            {
                Priority = "Urgent",
                Version = generated.Version,
            });
        allowedUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allowedUpdate = (await allowedUpdateResponse.Content
            .ReadFromJsonAsync<InstructionDetail>())!;
        allowedUpdate.Trips.Should().HaveCount(2);
        (await client.PutAsJsonAsync(
            $"/api/v1/load-instructions/{created.Id}",
            model with
            {
                RequestedQuantity = 40m,
                Version = allowedUpdate.Version,
            })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var history = await client.GetFromJsonAsync<InstructionHistoryItem[]>(
            $"/api/v1/load-instructions/{created.Id}/history");
        history!.Select(item => item.EventType).Should().Contain(
            ["LoadInstruction.Created", "LoadInstruction.Planned",
                "LoadInstruction.TripsGenerated", "LoadInstruction.Updated"]);
    }

    [Fact]
    public async Task RejectsInvalidContractDatesAndStaleConcurrency()
    {
        var client = await AdministratorClientAsync();
        var contract = await ActiveContractAsync(client);
        var invalid = InstructionModel(contract) with
        {
            CollectionWindowStart = DateTimeOffset.UtcNow.AddYears(2),
            CollectionWindowEnd = DateTimeOffset.UtcNow.AddYears(2).AddHours(1),
            DeliveryWindowStart = DateTimeOffset.UtcNow.AddYears(2).AddHours(2),
            DeliveryWindowEnd = DateTimeOffset.UtcNow.AddYears(2).AddHours(3),
        };
        (await client.PostAsJsonAsync("/api/v1/load-instructions", invalid))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var model = InstructionModel(contract);
        var created = await CreateAsync(client, model);
        var updatedResponse = await client.PutAsJsonAsync(
            $"/api/v1/load-instructions/{created.Id}",
            model with
            {
                Priority = "High",
                Version = created.Version,
            });
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(
            $"/api/v1/load-instructions/{created.Id}",
            model with
            {
                Priority = "Urgent",
                Version = created.Version,
            })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EnforcesPagingAndTenantQueryIsolation()
    {
        var client = await AdministratorClientAsync();
        var contract = await ActiveContractAsync(client);
        var created = await CreateAsync(client, InstructionModel(contract));
        (await client.GetAsync("/api/v1/load-instructions?pageSize=101"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var list = await client.GetFromJsonAsync<
            InstructionPagedResponse<InstructionListItem>>(
            "/api/v1/load-instructions?status=all&priority=all");
        list!.Items.Should().Contain(item => item.Id == created.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        context.Set(
            LoadIx.Domain.Common.TenantId.New(),
            Guid.NewGuid(),
            permissions: [LoadIxPermissions.TripView]);
        var service = scope.ServiceProvider
            .GetRequiredService<ILoadInstructionService>();
        var foreignRead = await service.GetAsync(created.Id, CancellationToken.None);
        foreignRead.Failure.Should().Be(InstructionFailure.NotFound);
    }

    private static async Task<ContractDetail> ActiveContractAsync(HttpClient client)
    {
        var routeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var model = new ContractWriteModel(
            DevelopmentSeedIds.CustomerIds[0],
            $"IC-{Guid.NewGuid():N}"[..18],
            "Instruction integration contract",
            today.AddDays(-30),
            today.AddDays(365),
            "ZAR",
            30,
            1m,
            null,
            [
                new ContractRouteWriteModel(
                    routeId,
                    "INSTRUCTION-ROUTE",
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
                    1000m,
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

    private static InstructionWriteModel InstructionModel(ContractDetail contract)
    {
        var collection = DateTimeOffset.UtcNow.AddDays(1);
        return new InstructionWriteModel(
            $"LI-{Guid.NewGuid():N}"[..18],
            contract.CustomerId,
            contract.Id,
            contract.Routes.Single().Id,
            contract.Commodities.Single().CommodityId,
            30m,
            "Tonnes",
            2,
            collection,
            collection.AddHours(2),
            collection.AddHours(4),
            collection.AddHours(8),
            "PO-SYNTHETIC",
            "CI-SYNTHETIC",
            "Normal",
            "Synthetic integration instruction");
    }

    private static async Task<InstructionDetail> CreateAsync(
        HttpClient client,
        InstructionWriteModel model)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/load-instructions", model);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstructionDetail>())!;
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
}
