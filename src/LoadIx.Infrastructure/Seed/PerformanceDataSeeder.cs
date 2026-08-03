using System.Data;
using LoadIx.Application.Security;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Fleet;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoadIx.Infrastructure.Seed;

public static class PerformanceSeedPlan
{
    public const int TenantCount = 10;
    public const int UserCount = 100;
    public const int DriverCount = 1_000;
    public const int VehicleCount = 1_000;
    public const int LoadInstructionCount = 10_000;
    public const int TripCount = 100_000;

    public const int UsersPerTenant = UserCount / TenantCount;
    public const int DriversPerTenant = DriverCount / TenantCount;
    public const int VehiclesPerTenant = VehicleCount / TenantCount;
    public const int InstructionsPerTenant = LoadInstructionCount / TenantCount;
    public const int TripsPerTenant = TripCount / TenantCount;
}

/// <summary>
/// Creates a large, deterministic, fictional workload only when the explicit
/// performance-seed command is used. The normal development seed never calls
/// this class.
/// </summary>
public sealed class PerformanceDataSeeder(
    LoadIxDbContext db,
    CurrentRequestContext requestContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    TimeProvider timeProvider,
    ILogger<PerformanceDataSeeder> logger)
{
    private const string TenantNamePrefix = "LoadIx Performance Tenant";
    private const int BulkBatchSize = 5_000;

    private static readonly Action<ILogger, int, int, int, Exception?> SeedMasterDataCompleted =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(1603, "PerformanceSeedCompleted"),
            "Performance seed completed with {TenantCount} tenants, {UserCount} users, and {DriverCount} drivers");
    private static readonly Action<ILogger, int, int, int, Exception?> SeedWorkloadCompleted =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(1604, "PerformanceSeedWorkloadCompleted"),
            "Performance seed created {VehicleCount} vehicles, {InstructionCount} load instructions, and {TripCount} trips");

    public async Task SeedAsync(string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                "A performance seed password of at least 12 characters is required through LOADIX_PERFORMANCE_SEED_PASSWORD.");
        }

        await db.Database.MigrateAsync(cancellationToken);
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(
                tenant => tenant.Name.StartsWith(TenantNamePrefix),
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Performance seed data already exists. Use a fresh database or remove the performance tenants explicitly before reseeding.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var tenants = await CreateTenantsAsync(now, cancellationToken);
        var tenantSeeds = new List<TenantSeed>(PerformanceSeedPlan.TenantCount);

        foreach (var tenant in tenants)
        {
            tenantSeeds.Add(
                await CreateTenantMasterDataAsync(tenant, now, cancellationToken));
        }

        foreach (var seed in tenantSeeds)
        {
            await CreateUsersAsync(seed, password, now, cancellationToken);
            await CreateLoadInstructionsAsync(seed, now, cancellationToken);
        }

        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        foreach (var seed in tenantSeeds)
        {
            await BulkInsertOperationalDataAsync(seed, now, connection, cancellationToken);
        }

        SeedMasterDataCompleted(
            logger,
            PerformanceSeedPlan.TenantCount,
            PerformanceSeedPlan.UserCount,
            PerformanceSeedPlan.DriverCount,
            null);
        SeedWorkloadCompleted(
            logger,
            PerformanceSeedPlan.VehicleCount,
            PerformanceSeedPlan.LoadInstructionCount,
            PerformanceSeedPlan.TripCount,
            null);
    }

    private async Task<IReadOnlyList<Tenant>> CreateTenantsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tenants = Enumerable.Range(1, PerformanceSeedPlan.TenantCount)
            .Select(number => Tenant.Create(
                TenantId.From(Id(100, number)),
                $"{TenantNamePrefix} {number:00}",
                $"{TenantNamePrefix} {number:00} (Pty) Ltd",
                $"PERF-TENANT-{number:00}",
                $"PERF-TAX-{number:00}",
                "ZAR",
                "Africa/Johannesburg").Value!)
            .ToArray();
        db.Tenants.AddRange(tenants);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return tenants;
    }

    private async Task<TenantSeed> CreateTenantMasterDataAsync(
        Tenant tenant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tenantNumber = int.Parse(
            tenant.Name[^2..],
            System.Globalization.CultureInfo.InvariantCulture);
        var tenantId = tenant.Id;
        var branchIds = new[] { Id(110, tenantNumber * 2 - 1), Id(110, tenantNumber * 2) };
        var customerIds = Enumerable.Range(1, 4)
            .Select(number => Id(120 + tenantNumber, number))
            .ToArray();
        var locationIds = Enumerable.Range(1, 4)
            .Select(number => Id(140 + tenantNumber, number))
            .ToArray();
        var commodityIds = Enumerable.Range(1, 3)
            .Select(number => Id(150 + tenantNumber, number))
            .ToArray();
        var contractId = Id(130 + tenantNumber, 1);
        var routeId = Id(131 + tenantNumber, 1);
        var rateId = Id(132 + tenantNumber, 1);

        requestContext.Set(tenantId, null);
        db.Branches.AddRange(
            Branch.Create(
                branchIds[0],
                tenantId,
                $"Performance North {tenantNumber:00}",
                $"P{tenantNumber:00}N",
                AddressAt("1 Synthetic North Road", "Johannesburg", "Gauteng", "2001"),
                $"north-{tenantNumber:00}@loadix.example.invalid",
                "+27 00 100 0001").Value!,
            Branch.Create(
                branchIds[1],
                tenantId,
                $"Performance South {tenantNumber:00}",
                $"P{tenantNumber:00}S",
                AddressAt("2 Synthetic South Road", "eMalahleni", "Mpumalanga", "1035"),
                $"south-{tenantNumber:00}@loadix.example.invalid",
                "+27 00 100 0002").Value!);

        for (var index = 0; index < customerIds.Length; index++)
        {
            var number = index + 1;
            db.Customers.Add(Customer.Create(
                customerIds[index],
                tenantId,
                $"Performance Customer {tenantNumber:00}-{number:00} (Pty) Ltd",
                $"Performance Customer {tenantNumber:00}-{number:00}",
                $"PERF-CUST-{tenantNumber:00}-{number:00}",
                $"PERF-CUST-TAX-{tenantNumber:00}-{number:00}",
                30,
                AddressAt("10 Synthetic Customer Road", "Johannesburg", "Gauteng", "2001")).Value!);
        }

        var sourceLocation = Location.Create(
            locationIds[0], tenantId, customerIds[0],
            $"Performance Mine {tenantNumber:00}",
            $"PERF-MINE-{tenantNumber:00}",
            LocationType.Mine,
            AddressAt("20 Synthetic Mine Road", "Rustenburg", "North West", "0300"),
            -25.667m, 27.243m, 500m, "Synthetic performance source site.").Value!;
        var destinationLocation = Location.Create(
            locationIds[1], tenantId, customerIds[0],
            $"Performance Depot {tenantNumber:00}",
            $"PERF-DEPOT-{tenantNumber:00}",
            LocationType.Depot,
            AddressAt("21 Synthetic Depot Road", "Johannesburg", "Gauteng", "2001"),
            -26.204m, 28.047m, 500m, "Synthetic performance destination site.").Value!;
        var weighbridge = Location.Create(
            locationIds[2], tenantId, null,
            $"Performance Weighbridge {tenantNumber:00}",
            $"PERF-WB-{tenantNumber:00}",
            LocationType.Weighbridge,
            AddressAt("22 Synthetic Weighbridge Road", "Johannesburg", "Gauteng", "2001"),
            -26.126m, 28.108m, 500m, "Synthetic performance weighbridge.").Value!;
        var secondaryLocation = Location.Create(
            locationIds[3], tenantId, customerIds[1],
            $"Performance Customer Site {tenantNumber:00}",
            $"PERF-SITE-{tenantNumber:00}",
            LocationType.Customer,
            AddressAt("23 Synthetic Customer Road", "Pretoria", "Gauteng", "0002"),
            -25.732m, 28.218m, 500m, "Synthetic performance customer site.").Value!;
        db.Locations.AddRange(sourceLocation, destinationLocation, weighbridge, secondaryLocation);

        foreach (var (id, name, code) in new[]
                 {
                     (commodityIds[0], "Performance Coal", "PERF-COAL"),
                     (commodityIds[1], "Performance Maize", "PERF-MAIZE"),
                     (commodityIds[2], "Performance Stone", "PERF-STONE"),
                 })
        {
            db.Commodities.Add(Commodity.Create(
                id, tenantId, name, $"{code}-{tenantNumber:00}", WeightUnit.Tonnes).Value!);
        }

        var contract = TransportContract.Create(
            contractId,
            tenantId,
            customerIds[0],
            $"PERF-CONTRACT-{tenantNumber:00}",
            $"Performance Contract {tenantNumber:00}",
            new DateOnly(2024, 1, 1),
            null,
            "ZAR",
            30,
            2.5m,
            "Synthetic performance contract.",
            [new ContractRouteDefinition(
                routeId, locationIds[0], locationIds[1], $"PERF-ROUTE-{tenantNumber:00}")],
            commodityIds,
            [new ContractRateDefinition(
                rateId,
                routeId,
                ContractRateType.SourceNetTonne,
                new DateOnly(2024, 1, 1),
                null,
                null,
                null,
                100m,
                null,
                5m)]).Value!;
        contract.Activate();
        db.TransportContracts.Add(contract);

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var driverIds = new Guid[PerformanceSeedPlan.DriversPerTenant];
        var vehicleIds = new Guid[PerformanceSeedPlan.VehiclesPerTenant];
        for (var index = 0; index < driverIds.Length; index++)
        {
            var number = index + 1;
            driverIds[index] = Id(160 + tenantNumber, number);
            db.Drivers.Add(Driver.Create(
                driverIds[index],
                tenantId,
                branchIds[index % branchIds.Length],
                "Performance",
                $"Driver {tenantNumber:00}-{number:000}",
                number % 5 == 0 ? DriverType.Contractor : DriverType.Employee,
                $"PERF-ID-{tenantNumber:00}-{number:000}",
                $"+27 00 2{tenantNumber:02} {number:0000}",
                $"PERF-LIC-{tenantNumber:00}-{number:000}",
                new DateOnly(2032, 12, 31),
                $"Performance Emergency {tenantNumber:00}-{number:000}",
                $"+27 00 3{tenantNumber:02} {number:0000}").Value!);
        }

        for (var index = 0; index < vehicleIds.Length; index++)
        {
            var number = index + 1;
            vehicleIds[index] = Id(170 + tenantNumber, number);
            db.Vehicles.Add(Vehicle.Create(
                vehicleIds[index],
                tenantId,
                branchIds[index % branchIds.Length],
                $"PERF-REG-{tenantNumber:00}-{number:000}",
                $"PERF-TRK-{tenantNumber:00}-{number:000}",
                "Synthetic Motors",
                "Bulk Hauler",
                2022 + number % 3,
                Weight.Create(8_500m, WeightUnit.Kilograms).Value!,
                Weight.Create(34_000m, WeightUnit.Kilograms).Value!,
                new DateOnly(2032, 6, 30),
                new DateOnly(2032, 12, 31)).Value!);
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return new TenantSeed(
            tenantNumber,
            tenantId,
            branchIds,
            customerIds,
            locationIds,
            commodityIds,
            contractId,
            routeId,
            rateId,
            driverIds,
            vehicleIds,
            []);
    }

    private async Task CreateUsersAsync(
        TenantSeed seed,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        requestContext.Set(seed.TenantId, null);
        var role = new ApplicationRole
        {
            Id = Id(200 + seed.TenantNumber, 1),
            TenantId = seed.TenantId,
            Name = $"{seed.TenantId.Value:N}:Performance-Administrator",
            DisplayName = "Performance Administrator",
        };
        role.SetPermissions(LoadIxPermissions.All);
        EnsureSucceeded(await roleManager.CreateAsync(role));

        var userIds = new Guid[PerformanceSeedPlan.UsersPerTenant];
        for (var index = 0; index < userIds.Length; index++)
        {
            var number = index + 1;
            var userId = Id(210 + seed.TenantNumber, number);
            var email = number == 1
                ? $"perf-admin-{seed.TenantNumber:00}@loadix.example.invalid"
                : $"perf-user-{seed.TenantNumber:00}-{number:00}@loadix.example.invalid";
            var user = new ApplicationUser
            {
                Id = userId,
                TenantId = seed.TenantId,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = $"Performance User {seed.TenantNumber:00}-{number:00}",
                IsActive = true,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                SecurityStamp = Id(220 + seed.TenantNumber, number).ToString("N"),
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password));
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role.Name!));
            userIds[index] = userId;
        }

        seed.UserIds = userIds;
        seed.AdminUserId = userIds[0];
        var driver = await db.Drivers.SingleAsync(
            item => item.Id == seed.DriverIds[0],
            cancellationToken);
        EnsureSucceeded(driver.LinkUser(seed.AdminUserId));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private async Task CreateLoadInstructionsAsync(
        TenantSeed seed,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        requestContext.Set(seed.TenantId, seed.AdminUserId);
        var instructionIds = new Guid[PerformanceSeedPlan.InstructionsPerTenant];
        for (var index = 0; index < instructionIds.Length; index++)
        {
            var number = index + 1;
            var id = Id(230 + seed.TenantNumber, number);
            instructionIds[index] = id;
            var start = now.AddDays(-30).AddHours(index % 24);
            var instruction = LoadInstruction.Create(
                id,
                seed.TenantId,
                new LoadInstructionDefinition(
                    $"PERF-LI-{seed.TenantNumber:00}-{number:0000}",
                    seed.CustomerIds[index % seed.CustomerIds.Length],
                    seed.ContractId,
                    seed.RouteId,
                    seed.CommodityIds[index % seed.CommodityIds.Length],
                    250m,
                    WeightUnit.Tonnes,
                    10,
                    start,
                    start.AddHours(4),
                    start.AddHours(5),
                    start.AddHours(16),
                    $"PERF-PO-{seed.TenantNumber:00}-{number:0000}",
                    $"PERF-INSTRUCTION-{seed.TenantNumber:00}-{number:0000}",
                    LoadInstructionPriority.Normal,
                    "Synthetic performance instruction.")).Value!;
            EnsureSucceeded(instruction.Plan());
            db.LoadInstructions.Add(instruction);
            if (number % BulkBatchSize == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        seed.InstructionIds = instructionIds;
    }

    private static async Task BulkInsertOperationalDataAsync(
        TenantSeed seed,
        DateTimeOffset now,
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var generatedTrips = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);
        var trips = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);
        var assignments = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);
        var transitions = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);
        var locations = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);
        var outbox = new List<Dictionary<string, object?>>(PerformanceSeedPlan.TripsPerTenant);

        for (var index = 0; index < PerformanceSeedPlan.TripsPerTenant; index++)
        {
            var number = index + 1;
            var instructionIndex = index / 10;
            var tripId = Id(240 + seed.TenantNumber, number);
            var instructionId = seed.InstructionIds[instructionIndex];
            var driverId = seed.DriverIds[index % seed.DriverIds.Length];
            var vehicleId = seed.VehicleIds[index % seed.VehicleIds.Length];
            var tripNumber = $"PERF-TRIP-{seed.TenantNumber:00}-{number:00000}";
            var collection = now.AddMinutes((index % 2_880) - 1_440);
            var delivery = collection.AddHours(8);
            var status = StatusFor(index);
            var assigned = collection.AddHours(-2);
            DateTimeOffset? dispatched = status is TripStatus.InTransit or TripStatus.Completed
                or TripStatus.ReconciliationRequired ? collection.AddHours(-1) : null;
            var collectionArrival = dispatched?.AddMinutes(30);
            var loadingStarted = collectionArrival?.AddMinutes(15);
            DateTimeOffset? collectionCompleted = status is TripStatus.InTransit or TripStatus.Completed
                or TripStatus.ReconciliationRequired ? collection.AddHours(1) : null;
            var inTransit = collectionCompleted?.AddMinutes(10);
            DateTimeOffset? deliveryArrival = status is TripStatus.Completed or TripStatus.ReconciliationRequired
                ? delivery.AddHours(-1) : null;
            var unloadingStarted = deliveryArrival?.AddMinutes(15);
            DateTimeOffset? deliveryCompleted = status is TripStatus.Completed or TripStatus.ReconciliationRequired
                ? delivery.AddMinutes(30) : null;
            DateTimeOffset? reconciliationSubmitted = status is TripStatus.Completed or TripStatus.ReconciliationRequired
                ? delivery.AddHours(1) : null;
            DateTimeOffset? completed = status == TripStatus.Completed
                ? now.AddMinutes(-(index % 1_440))
                : null;
            decimal? sourceGross = status == TripStatus.Planned ? null : 40_000m + index % 500;
            decimal? sourceTare = sourceGross.HasValue ? 10_000m : null;
            decimal? destinationGross = status is TripStatus.Completed or TripStatus.ReconciliationRequired
                ? 39_950m + index % 450 : null;
            decimal? destinationTare = destinationGross.HasValue ? 10_000m : null;
            var created = now.AddDays(-90).AddMinutes(index % 1_440);

            generatedTrips.Add(new Dictionary<string, object?>
            {
                ["Id"] = tripId,
                ["TenantId"] = seed.TenantId.Value,
                ["LoadInstructionId"] = instructionId,
                ["TripNumber"] = tripNumber,
                ["PlannedQuantity"] = 25m,
                ["CompletedQuantity"] = completed.HasValue ? 25m : null,
                ["QuantityUnit"] = WeightUnit.Tonnes.ToString(),
                ["Status"] = completed.HasValue ? GeneratedTripStatus.Completed.ToString() : GeneratedTripStatus.Planned.ToString(),
            });

            trips.Add(new Dictionary<string, object?>
            {
                ["Id"] = tripId,
                ["TenantId"] = seed.TenantId.Value,
                ["TripNumber"] = tripNumber,
                ["LoadInstructionId"] = instructionId,
                ["CustomerId"] = seed.CustomerIds[instructionIndex % seed.CustomerIds.Length],
                ["TransportContractId"] = seed.ContractId,
                ["ContractNumber"] = $"PERF-CONTRACT-{seed.TenantNumber:00}",
                ["ContractVersion"] = "1",
                ["ContractRouteId"] = seed.RouteId,
                ["RouteReference"] = $"PERF-ROUTE-{seed.TenantNumber:00}",
                ["CommodityId"] = seed.CommodityIds[index % seed.CommodityIds.Length],
                ["PlannedQuantity"] = 25m,
                ["QuantityUnit"] = WeightUnit.Tonnes.ToString(),
                ["RateId"] = seed.RateId,
                ["RateType"] = ContractRateType.SourceNetTonne.ToString(),
                ["RateEffectiveFrom"] = new DateOnly(2024, 1, 1),
                ["RateEffectiveTo"] = null,
                ["RateFixedAmount"] = null,
                ["RateBaseFee"] = null,
                ["RatePerTonneAmount"] = 100m,
                ["RateMinimumCharge"] = null,
                ["RateFuelSurchargePercent"] = 5m,
                ["RateRoundingDecimalPlaces"] = 2,
                ["RateRoundingMode"] = MidpointRounding.AwayFromZero.ToString(),
                ["RateCurrencyCode"] = "ZAR",
                ["DriverId"] = driverId,
                ["VehicleId"] = vehicleId,
                ["TrailerIds"] = string.Empty,
                ["PlannedCollectionAtUtc"] = collection,
                ["PlannedDeliveryAtUtc"] = delivery,
                ["Status"] = status.ToString(),
                ["OperationalNotes"] = string.Empty,
                ["CancellationOrRejectionReason"] = string.Empty,
                ["AssignedAtUtc"] = status == TripStatus.Planned ? null : assigned,
                ["DispatchedAtUtc"] = dispatched,
                ["CollectionArrivalAtUtc"] = collectionArrival,
                ["LoadingStartedAtUtc"] = loadingStarted,
                ["CollectionCompletedAtUtc"] = collectionCompleted,
                ["InTransitAtUtc"] = inTransit,
                ["DeliveryArrivalAtUtc"] = deliveryArrival,
                ["UnloadingStartedAtUtc"] = unloadingStarted,
                ["DeliveryCompletedAtUtc"] = deliveryCompleted,
                ["ReconciliationSubmittedAtUtc"] = reconciliationSubmitted,
                ["CompletedAtUtc"] = completed,
                ["CancelledAtUtc"] = null,
                ["RejectedAtUtc"] = null,
                ["SourceGrossWeight"] = sourceGross,
                ["SourceTareWeight"] = sourceTare,
                ["SourceWeighbridgeReference"] = sourceGross.HasValue ? $"PERF-SRC-{number:00000}" : null,
                ["DestinationGrossWeight"] = destinationGross,
                ["DestinationTareWeight"] = destinationTare,
                ["DestinationWeighbridgeReference"] = destinationGross.HasValue ? $"PERF-DST-{number:00000}" : null,
                ["IsReconciled"] = completed.HasValue,
                ["HasRequiredDocuments"] = completed.HasValue,
                ["CreatedAtUtc"] = created,
                ["CreatedByUserId"] = seed.AdminUserId,
                ["ModifiedAtUtc"] = now,
                ["ModifiedByUserId"] = seed.AdminUserId,
            });

            if (status != TripStatus.Planned)
            {
                assignments.Add(new Dictionary<string, object?>
                {
                    ["Id"] = Id(250 + seed.TenantNumber, number),
                    ["TenantId"] = seed.TenantId.Value,
                    ["TripId"] = tripId,
                    ["DriverId"] = driverId,
                    ["VehicleId"] = vehicleId,
                    ["TrailerIds"] = string.Empty,
                    ["ActorUserId"] = seed.AdminUserId,
                    ["AssignedAtUtc"] = assigned,
                    ["EndedAtUtc"] = completed,
                    ["Reason"] = "Synthetic performance assignment.",
                });
            }

            transitions.Add(new Dictionary<string, object?>
            {
                ["Id"] = Id(260 + seed.TenantNumber, number),
                ["TenantId"] = seed.TenantId.Value,
                ["TripId"] = tripId,
                ["PreviousStatus"] = TripStatus.Planned.ToString(),
                ["NewStatus"] = status.ToString(),
                ["ActorUserId"] = seed.AdminUserId,
                ["OccurredAtUtc"] = completed ?? delivery,
                ["Reason"] = "Synthetic performance transition.",
                ["IsManualOverride"] = false,
            });

            locations.Add(new Dictionary<string, object?>
            {
                ["Id"] = Id(270 + seed.TenantNumber, number),
                ["TenantId"] = seed.TenantId.Value,
                ["TripId"] = tripId,
                ["DriverUserId"] = seed.UserIds[0],
                ["Latitude"] = -26.204m + (index % 100) / 10_000m,
                ["Longitude"] = 28.047m + (index % 100) / 10_000m,
                ["AccuracyMetres"] = 25m,
                ["CapturedAtUtc"] = now.AddMinutes(-(index % 240)),
                ["ReceivedAtUtc"] = now.AddMinutes(-(index % 230)),
            });

            outbox.Add(new Dictionary<string, object?>
            {
                ["Id"] = Id(280 + seed.TenantNumber, number),
                ["TenantId"] = seed.TenantId.Value,
                ["TripId"] = tripId,
                ["EventType"] = "Trip.StatusChanged",
                ["Payload"] = $"{{\"tripId\":\"{tripId:D}\",\"status\":\"{status}\"}}",
                ["OccurredAtUtc"] = completed ?? delivery,
                ["DispatchedAtUtc"] = now,
                ["NextAttemptAtUtc"] = null,
                ["DeadLetteredAtUtc"] = null,
                ["Attempts"] = 1,
                ["LastError"] = null,
            });
        }

        await BulkInsertAsync(connection, "instructions", "GeneratedTrips", generatedTrips, cancellationToken);
        await BulkInsertAsync(connection, "trips", "Trips", trips, cancellationToken);
        await BulkInsertAsync(connection, "trips", "TripAssignments", assignments, cancellationToken);
        await BulkInsertAsync(connection, "trips", "TripTransitions", transitions, cancellationToken);
        await BulkInsertAsync(connection, "trips", "TripLocationUpdates", locations, cancellationToken);
        await BulkInsertAsync(connection, "operations", "OperationalOutboxMessages", outbox, cancellationToken);
    }

    private static TripStatus StatusFor(int index) =>
        (index % 20) switch
        {
            < 8 => TripStatus.Assigned,
            < 16 => TripStatus.InTransit,
            < 19 => TripStatus.Completed,
            _ => TripStatus.ReconciliationRequired,
        };

    private static async Task BulkInsertAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        List<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return;
        var sample = rows[0];
        var data = new DataTable();
        var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = $"SELECT TOP (0) * FROM [{schema}].[{tableName}]";
        await using (schemaCommand)
        await using (var reader = await schemaCommand.ExecuteReaderAsync(cancellationToken))
        {
            var schemaTable = reader.GetSchemaTable()
                ?? throw new InvalidOperationException($"Could not inspect {schema}.{tableName}.");
            foreach (var columnName in sample.Keys)
            {
                var schemaRow = schemaTable.Rows.Cast<DataRow>()
                    .SingleOrDefault(row => string.Equals(
                        row["ColumnName"].ToString(),
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"Column {schema}.{tableName}.{columnName} does not exist.");
                var columnType = (Type)schemaRow["DataType"];
                data.Columns.Add(new DataColumn(columnName, columnType)
                {
                    AllowDBNull = true,
                });
            }
        }

        foreach (var row in rows)
        {
            var values = sample.Keys
                .Select(column => ToDbValue(row[column]))
                .ToArray();
            data.Rows.Add(values);
        }

        using var bulk = new SqlBulkCopy(
            connection,
            SqlBulkCopyOptions.TableLock,
            null)
        {
            DestinationTableName = $"[{schema}].[{tableName}]",
            BatchSize = BulkBatchSize,
            BulkCopyTimeout = 0,
        };
        foreach (var column in sample.Keys)
        {
            bulk.ColumnMappings.Add(column, column);
        }

        await bulk.WriteToServerAsync(data, cancellationToken);
    }

    private static object ToDbValue(object? value) => value switch
    {
        null => DBNull.Value,
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        _ => value,
    };

    private static Address AddressAt(
        string line1,
        string city,
        string province,
        string postalCode) =>
        Address.Create(line1, null, city, province, postalCode, "ZA").Value!;

    private static Guid Id(int family, int number)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(family).CopyTo(bytes, 0);
        BitConverter.GetBytes(number).CopyTo(bytes, 4);
        BitConverter.GetBytes(0x17).CopyTo(bytes, 8);
        BitConverter.GetBytes(0x20260803).CopyTo(bytes, 12);
        return new Guid(bytes);
    }

    private static void EnsureSucceeded(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Performance seed domain data was invalid: {result.Error!.Code}.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }

    private sealed class TenantSeed(
        int tenantNumber,
        TenantId tenantId,
        Guid[] branchIds,
        Guid[] customerIds,
        Guid[] locationIds,
        Guid[] commodityIds,
        Guid contractId,
        Guid routeId,
        Guid rateId,
        Guid[] driverIds,
        Guid[] vehicleIds,
        Guid[] userIds,
        Guid adminUserId = default)
    {
        public int TenantNumber { get; } = tenantNumber;
        public TenantId TenantId { get; } = tenantId;
        public Guid[] BranchIds { get; } = branchIds;
        public Guid[] CustomerIds { get; } = customerIds;
        public Guid[] LocationIds { get; } = locationIds;
        public Guid[] CommodityIds { get; } = commodityIds;
        public Guid ContractId { get; } = contractId;
        public Guid RouteId { get; } = routeId;
        public Guid RateId { get; } = rateId;
        public Guid[] DriverIds { get; } = driverIds;
        public Guid[] VehicleIds { get; } = vehicleIds;
        public Guid[] UserIds { get; set; } = userIds;
        public Guid AdminUserId { get; set; } = adminUserId;
        public Guid[] InstructionIds { get; set; } = [];
    }
}
