using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LoadIx.Api.Endpoints;
using LoadIx.Application.Contracts;
using LoadIx.Application.Finance;
using LoadIx.Application.Instructions;
using LoadIx.Application.MasterData;
using LoadIx.Application.Reporting;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using LoadIx.Domain.Common;
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
    public async Task EveryReportEndpointExecutesAgainstSqlServer()
    {
        var client = await AdministratorClientAsync();
        var reports = new[]
        {
            "trip-register", "tonnage-by-customer", "tonnage-by-commodity",
            "tonnage-by-route", "variance", "driver-performance",
            "vehicle-utilisation", "contract-performance", "customer-revenue",
            "trip-profitability", "exceptions", "outstanding-pod", "expiries",
            "invoice-status",
        };

        foreach (var report in reports)
        {
            var response = await client.GetAsync(
                $"/api/v1/reports/{report}?page=1&pageSize=10");

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                $"report {report} should execute against SQL Server");
        }
    }

    [Fact]
    public async Task ReportPaginationAndFiltersReturnTrueCountsAndStablePages()
    {
        var client = await AdministratorClientAsync();
        var generated = await GenerateTripsAsync(client, 3);

        var pageOne = await client.GetFromJsonAsync<ReportingPagedResponse<TripRegisterRow>>(
            "/api/v1/reports/trip-register?page=1&pageSize=2");
        var pageTwo = await client.GetFromJsonAsync<ReportingPagedResponse<TripRegisterRow>>(
            "/api/v1/reports/trip-register?page=2&pageSize=2");
        var customer = await client.GetFromJsonAsync<ReportingPagedResponse<TripRegisterRow>>(
            $"/api/v1/reports/trip-register?customerId={DevelopmentSeedIds.CustomerIds[1]:D}&page=1&pageSize=10");

        pageOne!.TotalCount.Should().Be(3);
        pageOne.TotalPages.Should().Be(2);
        pageOne.Items.Should().HaveCount(2);
        pageTwo!.TotalCount.Should().Be(3);
        pageTwo.Items.Should().ContainSingle();
        pageOne.Items.Select(item => item.TripId)
            .Intersect(pageTwo.Items.Select(item => item.TripId))
            .Should().BeEmpty();
        pageOne.Items.Select(item => item.PlannedDeliveryAtUtc)
            .Should().BeInDescendingOrder();
        customer!.TotalCount.Should().Be(0);
        generated.Should().HaveCount(3);
    }

    [Fact]
    public async Task SynchronousCsvExportIncludesHeadersAndProtectsFormulaCells()
    {
        var client = await AdministratorClientAsync();
        var vehicle = await CreateFormulaVehicleAsync(client);
        var generated = await GenerateTripsAsync(client, 1);
        var resources = await ResourcesAsync(client);
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;
        await SendTripCommandAsync(client, trip.Id, "assign",
            new AssignTripModel(trip.Version, resources.DriverIds[0], vehicle.Id,
                [resources.TrailerIds[0]]), "csv-formula-assign");

        var csv = await client.GetAsync(
            "/api/v1/reports/trip-register/export.csv?page=1&pageSize=100");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await csv.Content.ReadAsStringAsync();
        content.Should().StartWith("Trip Number,");
        content.Should().Contain("'=FORMULA-FLEET");

        var many = await GenerateTripsAsync(client, 101);
        many.Should().HaveCount(101);
        var tooLarge = await client.GetAsync(
            "/api/v1/reports/trip-register/export.csv?page=1&pageSize=100");
        tooLarge.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await tooLarge.Content.ReadAsStringAsync())
            .Should().Contain("report.export_requires_background");
    }

    [Fact]
    public async Task ExportStatusAndDownloadAreTenantScoped()
    {
        var client = await AdministratorClientAsync();
        var enqueue = await client.PostAsync(
            "/api/v1/reports/trip-register/export-jobs?page=1&pageSize=100", null);
        enqueue.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var job = (await enqueue.Content
            .ReadFromJsonAsync<ReportExportJobResponse>())!;

        var status = await client.GetAsync(
            $"/api/v1/reports/export-jobs/{job.JobId:D}");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        var download = await client.GetAsync(
            $"/api/v1/reports/export-jobs/{job.JobId:D}/download");
        download.StatusCode.Should().BeOneOf(
            HttpStatusCode.Conflict, HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var requestContext = scope.ServiceProvider
            .GetRequiredService<CurrentRequestContext>();
        requestContext.Set(TenantId.New(), Guid.NewGuid(),
            permissions: [LoadIxPermissions.ReportView]);
        var queue = scope.ServiceProvider.GetRequiredService<IReportExportService>();
        queue.GetJob(job.JobId, requestContext.TenantId!.Value).Should().BeNull();
        queue.GetContent(job.JobId, requestContext.TenantId.Value).Should().BeNull();
    }

    [Fact]
    public async Task ReportRowsAndDashboardAreTenantScoped()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var requestContext = scope.ServiceProvider
            .GetRequiredService<CurrentRequestContext>();
        requestContext.Set(TenantId.New(), Guid.NewGuid(),
            permissions: [LoadIxPermissions.ReportView]);
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();

        var dashboard = await service.GetDashboardKpisAsync(
            new ReportingFilter(), CancellationToken.None);
        var rows = await service.GetTripRegisterAsync(
            new ReportingFilter(), CancellationToken.None);

        dashboard.ActiveTrips.Should().Be(0);
        dashboard.Revenue.Should().Be(0m);
        rows.Items.Should().BeEmpty();
        rows.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task InvoiceStatusRespectsBranchAndCustomerFiltersWithoutCountingWholeMixedBatch()
    {
        var client = await AdministratorClientAsync();
        (await client.PutAsJsonAsync(
            "/api/v1/finance/tax-configuration",
            new TenantTaxConfigurationWriteModel("StandardRated", 15m)))
            .EnsureSuccessStatusCode();

        var contract = await ActiveContractAsync(client);
        var generated = await GenerateTripsAsync(client, 2, contract);
        var resources = await ResourcesAsync(client);
        var financialIds = new List<Guid>();
        for (var index = 0; index < generated.Length; index++)
        {
            var trip = await CompleteTripAsync(
                client,
                generated[index].Id,
                resources,
                index == 0 ? 0 : 6,
                $"invoice-branch-{index}");
            var financial = await client.PostAsync(
                $"/api/v1/finance/trips/{trip.Id}/recalculate", null);
            financial.EnsureSuccessStatusCode();
            var details = (await financial.Content
                .ReadFromJsonAsync<TripFinancialsDetail>())!;
            var approval = await client.PostAsJsonAsync(
                $"/api/v1/finance/trips/{trip.Id}/approve",
                new ApproveFinancialsModel(details.Version, "Reporting branch test"));
            approval.EnsureSuccessStatusCode();
            financialIds.Add(details.Id);
        }

        var create = await client.PostAsJsonAsync(
            "/api/v1/finance/invoice-batches",
            new InvoiceBatchCreateModel(financialIds));
        create.EnsureSuccessStatusCode();

        var all = await InvoiceStatusAsync(client);
        var branchA = await InvoiceStatusAsync(client, DevelopmentSeedIds.JohannesburgBranchId);
        var branchB = await InvoiceStatusAsync(client, DevelopmentSeedIds.MpumalangaBranchId);
        var combined = await InvoiceStatusAsync(
            client,
            DevelopmentSeedIds.JohannesburgBranchId,
            contract.CustomerId);

        all.Should().ContainSingle(item => item.Status == "Draft")
            .Which.Should().Match<InvoiceStatusRow>(item =>
                item.BatchCount == 1 && item.Total == 2_875m);
        branchA.Should().ContainSingle(item => item.Status == "Draft")
            .Which.Should().Match<InvoiceStatusRow>(item =>
                item.BatchCount == 1 && item.Total == 1_437.5m);
        branchB.Should().ContainSingle(item => item.Status == "Draft")
            .Which.Should().Match<InvoiceStatusRow>(item =>
                item.BatchCount == 1 && item.Total == 1_437.5m);
        combined.Should().BeEquivalentTo(branchA);
    }

    [Fact]
    public async Task ExpiryReportSupportsUnboundedOneSidedAndTenantLocalDateRanges()
    {
        var client = await AdministratorClientAsync();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "Africa/Johannesburg").DateTime.Date);
        var historical = await CreateExpiryDriverAsync(client, "Historical", today.AddDays(-60));
        var yesterday = await CreateExpiryDriverAsync(client, "Yesterday", today.AddDays(-1));
        var todayDriver = await CreateExpiryDriverAsync(client, "Today", today);
        var thirtyDays = await CreateExpiryDriverAsync(client, "ThirtyDays", today.AddDays(30));
        var thirtyOneDays = await CreateExpiryDriverAsync(client, "ThirtyOneDays", today.AddDays(31));
        var distant = await CreateExpiryDriverAsync(client, "Distant", today.AddDays(180));

        var noDates = await ExpiryAsync(client);
        noDates.Items.Select(item => item.Resource)
            .Should().Contain(todayDriver).And.Contain(thirtyDays).And.Contain(thirtyOneDays)
            .And.NotContain(yesterday).And.NotContain(historical).And.NotContain(distant);

        var fromOnly = await ExpiryAsync(client, from: today.AddDays(31));
        fromOnly.Items.Select(item => item.Resource)
            .Should().Contain(thirtyOneDays).And.Contain(distant)
            .And.NotContain(todayDriver).And.NotContain(thirtyDays);

        var toOnly = await ExpiryAsync(client, to: today.AddDays(-1));
        toOnly.Items.Select(item => item.Resource)
            .Should().Contain(historical).And.Contain(yesterday)
            .And.NotContain(todayDriver);

        var historicalRange = await ExpiryAsync(
            client, today.AddDays(-60), today.AddDays(-30));
        historicalRange.Items.Select(item => item.Resource)
            .Should().Contain(historical).And.NotContain(yesterday);

        var futureRange = await ExpiryAsync(
            client, today.AddDays(31), today.AddDays(180));
        futureRange.Items.Select(item => item.Resource)
            .Should().Contain(thirtyOneDays).And.Contain(distant)
            .And.NotContain(todayDriver);

        var reversed = await client.GetAsync(
            $"/api/v1/reports/expiries?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}");
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProductionToleranceQueryCoversActiveExceptionAndExceptionReport()
    {
        var client = await AdministratorClientAsync();
        var generated = await GenerateTripsAsync(client, 1);
        var resources = await ResourcesAsync(client);
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{generated[0].Id}"))!;
        trip = await SendTripCommandAsync(client, trip.Id, "assign",
            new AssignTripModel(trip.Version, resources.DriverIds[1],
                resources.VehicleIds[1], [resources.TrailerIds[1]]), "tolerance-assign");
        trip = await SendTripCommandAsync(client, trip.Id, "dispatch",
            new TripCommandModel(trip.Version), "tolerance-dispatch");
        trip = await SendTripCommandAsync(client, trip.Id, "collection-arrival",
            new TripCommandModel(trip.Version), "tolerance-arrival");
        trip = await SendTripCommandAsync(client, trip.Id, "start-loading",
            new TripCommandModel(trip.Version), "tolerance-loading");
        await SendTripCommandAsync(client, trip.Id, "source-weighbridge",
            new TripWeighbridgeModel(trip.Version, 43_600m, 12_000m, "TOL-SRC"),
            "tolerance-source");

        var dashboard = await client.GetFromJsonAsync<DashboardKpis>(
            "/api/v1/reports/dashboard");
        var exceptions = await client.GetFromJsonAsync<ReportingPagedResponse<ExceptionReportRow>>(
            "/api/v1/reports/exceptions?page=1&pageSize=100");

        dashboard!.TripsOutsideTolerance.Should().Be(1);
        exceptions!.Items.Should().ContainSingle(item =>
            item.TripId == trip.Id && item.Reason == "Outside tolerance");
    }

    [Fact]
    public async Task DashboardCalculatesOperationalFinancialAndExpiryKpis()
    {
        var client = await AdministratorClientAsync();
        (await client.PutAsJsonAsync(
            "/api/v1/finance/tax-configuration",
            new TenantTaxConfigurationWriteModel("StandardRated", 15m)))
            .EnsureSuccessStatusCode();
        var resources = await ResourcesAsync(client);

        var completed = await CompleteTripAsync(
            client,
            (await GenerateTripsAsync(client, 1))[0].Id,
            resources,
            0,
            "kpi-completed");
        var financialResponse = await client.PostAsync(
            $"/api/v1/finance/trips/{completed.Id}/recalculate", null);
        financialResponse.EnsureSuccessStatusCode();

        var delayed = (await GenerateTripsAsync(
            client, 1, collectionAt: DateTimeOffset.UtcNow.AddDays(-2)))[0];
        var outside = (await GenerateTripsAsync(client, 1))[0];
        var outsideTrip = await StartLoadingTripAsync(
            client, outside.Id, resources, 1, "kpi-outside");
        await SendTripCommandAsync(client, outsideTrip.Id, "source-weighbridge",
            new TripWeighbridgeModel(outsideTrip.Version, 43_600m, 12_000m, "KPI-OUTSIDE"),
            "kpi-outside-source");

        var awaiting = (await GenerateTripsAsync(client, 1))[0];
        await SubmitReconciliationTripAsync(
            client, awaiting.Id, resources, 2, "kpi-awaiting");
        var expiryDriver = await CreateExpiryDriverAsync(
            client,
            "KpiToday",
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "Africa/Johannesburg").DateTime.Date));

        var dashboard = await client.GetFromJsonAsync<DashboardKpis>(
            "/api/v1/reports/dashboard");

        dashboard!.ActiveTrips.Should().Be(3);
        dashboard.CompletedTripsToday.Should().Be(1);
        dashboard.TonnesDeliveredToday.Should().Be(30m);
        dashboard.AwaitingReconciliation.Should().Be(1);
        dashboard.TripsOutsideTolerance.Should().Be(1);
        dashboard.DelayedTrips.Should().Be(1);
        dashboard.FleetUtilisationPercent.Should().Be(10m);
        dashboard.Revenue.Should().Be(1_250m);
        dashboard.EstimatedGrossMargin.Should().Be(1_250m);
        dashboard.CustomerConcentrationPercent.Should().Be(100m);
        dashboard.UpcomingLicenceExpiries.Should().Be(1);
        delayed.Id.Should().NotBe(Guid.Empty);
        expiryDriver.Should().Contain("KpiToday");
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
        int count,
        ContractDetail? contract = null,
        DateTimeOffset? collectionAt = null)
    {
        contract ??= await ActiveContractAsync(client);
        var collection = collectionAt ?? DateTimeOffset.UtcNow.AddDays(1);
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

    private static async Task<ContractDetail> ActiveContractAsync(
        HttpClient client,
        Guid? customerId = null)
    {
        var routeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var model = new ContractWriteModel(
            customerId ?? DevelopmentSeedIds.CustomerIds[0],
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

    private static async Task<TripDetail> CompleteTripAsync(
        HttpClient client,
        Guid tripId,
        ResourceIds resources,
        int resourceIndex,
        string keyPrefix)
    {
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{tripId}"))!;
        trip = await SendTripCommandAsync(client, trip.Id, "assign",
            new AssignTripModel(trip.Version, resources.DriverIds[resourceIndex],
                resources.VehicleIds[resourceIndex], [resources.TrailerIds[resourceIndex]]),
            $"{keyPrefix}-assign");
        trip = await SendTripCommandAsync(client, trip.Id, "dispatch",
            new TripCommandModel(trip.Version), $"{keyPrefix}-dispatch");
        foreach (var route in new[] { "collection-arrival", "start-loading" })
        {
            trip = await SendTripCommandAsync(client, trip.Id, route,
                new TripCommandModel(trip.Version), $"{keyPrefix}-{route}");
        }

        trip = await SendTripCommandAsync(client, trip.Id, "source-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42_000m, 12_000m,
                $"{keyPrefix}-source"), $"{keyPrefix}-source");
        trip = await SendTripCommandAsync(client, trip.Id, "complete-collection",
            new TripCommandModel(trip.Version), $"{keyPrefix}-complete-collection");
        trip = await SendTripCommandAsync(client, trip.Id, "depart-collection",
            new TripCommandModel(trip.Version), $"{keyPrefix}-depart");
        trip = await SendTripCommandAsync(client, trip.Id, "delivery-arrival",
            new TripCommandModel(trip.Version), $"{keyPrefix}-delivery-arrival");
        trip = await SendTripCommandAsync(client, trip.Id, "start-unloading",
            new TripCommandModel(trip.Version), $"{keyPrefix}-start-unloading");
        trip = await SendTripCommandAsync(client, trip.Id, "destination-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42_000m, 12_000m,
                $"{keyPrefix}-destination"), $"{keyPrefix}-destination");
        foreach (var route in new[]
                 {
                     "complete-delivery", "submit-reconciliation",
                     "confirm-reconciliation", "confirm-required-documents", "complete",
                 })
        {
            trip = await SendTripCommandAsync(client, trip.Id, route,
                new TripCommandModel(trip.Version), $"{keyPrefix}-{route}");
        }

        return trip;
    }

    private static async Task<TripDetail> StartLoadingTripAsync(
        HttpClient client,
        Guid tripId,
        ResourceIds resources,
        int resourceIndex,
        string keyPrefix)
    {
        var trip = (await client.GetFromJsonAsync<TripDetail>(
            $"/api/v1/trips/{tripId}"))!;
        trip = await SendTripCommandAsync(client, trip.Id, "assign",
            new AssignTripModel(trip.Version, resources.DriverIds[resourceIndex],
                resources.VehicleIds[resourceIndex], [resources.TrailerIds[resourceIndex]]),
            $"{keyPrefix}-assign");
        trip = await SendTripCommandAsync(client, trip.Id, "dispatch",
            new TripCommandModel(trip.Version), $"{keyPrefix}-dispatch");
        trip = await SendTripCommandAsync(client, trip.Id, "collection-arrival",
            new TripCommandModel(trip.Version), $"{keyPrefix}-arrival");
        return await SendTripCommandAsync(client, trip.Id, "start-loading",
            new TripCommandModel(trip.Version), $"{keyPrefix}-loading");
    }

    private static async Task<TripDetail> SubmitReconciliationTripAsync(
        HttpClient client,
        Guid tripId,
        ResourceIds resources,
        int resourceIndex,
        string keyPrefix)
    {
        var trip = await StartLoadingTripAsync(
            client, tripId, resources, resourceIndex, keyPrefix);
        trip = await SendTripCommandAsync(client, trip.Id, "source-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42_000m, 12_000m,
                $"{keyPrefix}-source"), $"{keyPrefix}-source");
        trip = await SendTripCommandAsync(client, trip.Id, "complete-collection",
            new TripCommandModel(trip.Version), $"{keyPrefix}-complete-collection");
        trip = await SendTripCommandAsync(client, trip.Id, "depart-collection",
            new TripCommandModel(trip.Version), $"{keyPrefix}-depart");
        trip = await SendTripCommandAsync(client, trip.Id, "delivery-arrival",
            new TripCommandModel(trip.Version), $"{keyPrefix}-delivery-arrival");
        trip = await SendTripCommandAsync(client, trip.Id, "start-unloading",
            new TripCommandModel(trip.Version), $"{keyPrefix}-start-unloading");
        trip = await SendTripCommandAsync(client, trip.Id, "destination-weighbridge",
            new TripWeighbridgeModel(trip.Version, 42_000m, 12_000m,
                $"{keyPrefix}-destination"), $"{keyPrefix}-destination");
        trip = await SendTripCommandAsync(client, trip.Id, "complete-delivery",
            new TripCommandModel(trip.Version), $"{keyPrefix}-complete-delivery");
        return await SendTripCommandAsync(client, trip.Id, "submit-reconciliation",
            new TripCommandModel(trip.Version), $"{keyPrefix}-submit-reconciliation");
    }

    private static async Task<IReadOnlyList<InvoiceStatusRow>> InvoiceStatusAsync(
        HttpClient client,
        Guid? branchId = null,
        Guid? customerId = null)
    {
        var query = new List<string> { "page=1", "pageSize=100" };
        if (branchId.HasValue) query.Add($"branchId={branchId.Value:D}");
        if (customerId.HasValue) query.Add($"customerId={customerId.Value:D}");
        var response = await client.GetFromJsonAsync<ReportingPagedResponse<InvoiceStatusRow>>(
            $"/api/v1/reports/invoice-status?{string.Join('&', query)}");
        return response!.Items;
    }

    private static async Task<ReportingPagedResponse<ExpiryReportRow>> ExpiryAsync(
        HttpClient client,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        var query = new List<string> { "page=1", "pageSize=100" };
        if (from.HasValue) query.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) query.Add($"to={to.Value:yyyy-MM-dd}");
        return (await client.GetFromJsonAsync<ReportingPagedResponse<ExpiryReportRow>>(
            $"/api/v1/reports/expiries?{string.Join('&', query)}"))!;
    }

    private static async Task<string> CreateExpiryDriverAsync(
        HttpClient client,
        string name,
        DateOnly expiry)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/drivers",
            new DriverWriteModel(
                DevelopmentSeedIds.JohannesburgBranchId,
                "Expiry",
                $"{name}-{Guid.NewGuid():N}"[..18],
                "Employee",
                $"EXP-{Guid.NewGuid():N}"[..16],
                "+27 00 099 0001",
                $"EXP-LIC-{Guid.NewGuid():N}"[..16],
                expiry,
                "Expiry Contact",
                "+27 00 099 0002"));
        response.EnsureSuccessStatusCode();
        var item = (await response.Content.ReadFromJsonAsync<DriverItem>())!;
        return $"{item.FirstName} {item.LastName}";
    }

    private static async Task<VehicleItem> CreateFormulaVehicleAsync(
        HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/vehicles",
            new VehicleWriteModel(
                DevelopmentSeedIds.JohannesburgBranchId,
                "FORMULA-REG",
                "=FORMULA-FLEET",
                "Test Motors",
                "Formula Test Hauler",
                2026,
                8_500m,
                34_000m,
                "Kilograms",
                new DateOnly(2030, 12, 31),
                new DateOnly(2030, 6, 30),
                true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VehicleItem>())!;
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
