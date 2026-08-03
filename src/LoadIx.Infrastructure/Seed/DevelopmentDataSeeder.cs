using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Fleet;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Seed;

public sealed class DevelopmentDataSeeder(
    LoadIxDbContext dbContext,
    CurrentRequestContext requestContext)
{
    public Task SeedAsync(CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SeedWithinTransactionAsync(cancellationToken));

    private async Task SeedWithinTransactionAsync(
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!await dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(
                    tenant => tenant.Id == DevelopmentSeedIds.TenantId,
                    cancellationToken))
        {
            dbContext.Tenants.Add(CreateTenant());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        requestContext.Set(
            DevelopmentSeedIds.TenantId,
            DevelopmentSeedIds.ActorId);

        await AddBranchesAsync(cancellationToken);
        await AddCustomersAsync(cancellationToken);
        await AddUserProfilesAsync(cancellationToken);
        await AddContactsAsync(cancellationToken);
        await AddLocationsAsync(cancellationToken);
        await AddCommoditiesAsync(cancellationToken);
        await AddDriversAsync(cancellationToken);
        await AddVehiclesAsync(cancellationToken);
        await AddTrailersAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AddBranchesAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Branches.IgnoreQueryFilters().AnyAsync(
                branch => branch.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        dbContext.Branches.AddRange(
            Branch.Create(
                DevelopmentSeedIds.JohannesburgBranchId,
                DevelopmentSeedIds.TenantId,
                "Johannesburg Operations",
                "JHB",
                AddressAt("1 Demo Logistics Road", "Johannesburg", "Gauteng", "2001"),
                "operations@example.invalid",
                "+27 00 000 0101").Value,
            Branch.Create(
                DevelopmentSeedIds.MpumalangaBranchId,
                DevelopmentSeedIds.TenantId,
                "Mpumalanga Operations",
                "MPU",
                AddressAt("2 Demo Haulage Avenue", "eMalahleni", "Mpumalanga", "1035"),
                "mpumalanga@example.invalid",
                "+27 00 000 0102").Value);
    }

    private async Task AddCustomersAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                customer => customer.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        dbContext.Customers.AddRange(
            Customer.Create(
                DevelopmentSeedIds.CustomerIds[0],
                DevelopmentSeedIds.TenantId,
                "Demo Mining Resources (Pty) Ltd",
                "Demo Mining",
                "DEMO-CUST-001",
                "DEMO-TAX-001",
                30,
                AddressAt("10 Sample Mine Road", "Rustenburg", "North West", "0300"))
                .Value,
            Customer.Create(
                DevelopmentSeedIds.CustomerIds[1],
                DevelopmentSeedIds.TenantId,
                "Sample Agricultural Co-operative",
                "Sample Agriculture",
                "DEMO-CUST-002",
                "DEMO-TAX-002",
                21,
                AddressAt("20 Example Silo Lane", "Bothaville", "Free State", "9660"))
                .Value,
            Customer.Create(
                DevelopmentSeedIds.CustomerIds[2],
                DevelopmentSeedIds.TenantId,
                "Example Construction Materials (Pty) Ltd",
                "Example Materials",
                "DEMO-CUST-003",
                "DEMO-TAX-003",
                30,
                AddressAt("30 Fictional Quarry Way", "Pretoria", "Gauteng", "0002"))
                .Value);
    }

    private async Task AddUserProfilesAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.UserProfiles.IgnoreQueryFilters().AnyAsync(
                profile => profile.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        dbContext.UserProfiles.Add(UserProfile.Create(
            DevelopmentSeedIds.ActorId,
            DevelopmentSeedIds.TenantId,
            DevelopmentSeedIds.JohannesburgBranchId,
            "Demo",
            "Administrator",
            "admin@example.invalid",
            "+27 00 000 0200").Value);
    }

    private async Task AddContactsAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.ContactPeople.IgnoreQueryFilters().AnyAsync(
                contact => contact.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        var contacts = new List<ContactPerson>();
        for (var customerIndex = 0; customerIndex < DevelopmentSeedIds.CustomerIds.Length; customerIndex++)
        {
            var customerNumber = customerIndex + 1;
            contacts.Add(ContactPerson.Create(
                DeterministicGuid(130, customerNumber),
                DevelopmentSeedIds.TenantId,
                DevelopmentSeedIds.CustomerIds[customerIndex],
                $"Demo Operations Contact {customerNumber:00}",
                $"operations{customerNumber:00}@example.invalid",
                $"+27 00 001 {customerNumber:0000}",
                ContactCategory.Operational).Value);
            contacts.Add(ContactPerson.Create(
                DeterministicGuid(131, customerNumber),
                DevelopmentSeedIds.TenantId,
                DevelopmentSeedIds.CustomerIds[customerIndex],
                $"Demo Finance Contact {customerNumber:00}",
                $"finance{customerNumber:00}@example.invalid",
                $"+27 00 002 {customerNumber:0000}",
                ContactCategory.Finance).Value);
        }

        dbContext.ContactPeople.AddRange(contacts);
    }

    private async Task AddLocationsAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Locations.IgnoreQueryFilters().AnyAsync(
                location => location.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        var locationData = new[]
        {
            (1, "Demo Mine North", "MINE-N", LocationType.Mine, -25.667m, 27.243m, DevelopmentSeedIds.CustomerIds[0]),
            (2, "Demo Mine South", "MINE-S", LocationType.Mine, -25.702m, 27.312m, DevelopmentSeedIds.CustomerIds[0]),
            (3, "Sample Grain Silo", "SILO-1", LocationType.Warehouse, -27.388m, 26.619m, DevelopmentSeedIds.CustomerIds[1]),
            (4, "Example Quarry", "QUARRY-1", LocationType.Customer, -25.732m, 28.218m, DevelopmentSeedIds.CustomerIds[2]),
            (5, "Johannesburg Depot", "DEPOT-JHB", LocationType.Depot, -26.204m, 28.047m, (Guid?)null),
            (6, "Central Weighbridge", "WB-CENTRAL", LocationType.Weighbridge, -26.126m, 28.108m, (Guid?)null),
        };

        foreach (var item in locationData)
        {
            dbContext.Locations.Add(Location.Create(
                DeterministicGuid(140, item.Item1),
                DevelopmentSeedIds.TenantId,
                item.Item7,
                item.Item2,
                item.Item3,
                item.Item4,
                AddressAt(
                    $"{item.Item1} Synthetic Site Road",
                    "Demo City",
                    "Demo Province",
                    $"10{item.Item1:00}"),
                item.Item5,
                item.Item6,
                500m,
                "Synthetic development site. Follow controller instructions.")
                .Value);
        }
    }

    private async Task AddCommoditiesAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Commodities.IgnoreQueryFilters().AnyAsync(
                commodity => commodity.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        var commodities = new[]
        {
            ("Coal", "COAL"),
            ("Maize", "MAIZE"),
            ("Aggregate Stone", "STONE"),
            ("River Sand", "SAND"),
        };

        for (var index = 0; index < commodities.Length; index++)
        {
            dbContext.Commodities.Add(Commodity.Create(
                DeterministicGuid(150, index + 1),
                DevelopmentSeedIds.TenantId,
                commodities[index].Item1,
                commodities[index].Item2,
                WeightUnit.Tonnes).Value);
        }
    }

    private async Task AddDriversAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Drivers.IgnoreQueryFilters().AnyAsync(
                driver => driver.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        for (var number = 1; number <= 10; number++)
        {
            dbContext.Drivers.Add(Driver.Create(
                DeterministicGuid(160, number),
                DevelopmentSeedIds.TenantId,
                number <= 6
                    ? DevelopmentSeedIds.JohannesburgBranchId
                    : DevelopmentSeedIds.MpumalangaBranchId,
                "Demo",
                $"Driver {number:00}",
                number <= 8 ? DriverType.Employee : DriverType.Contractor,
                $"DEMO-ID-REF-{number:000}",
                $"+27 00 010 {number:0000}",
                $"DEMO-LIC-{number:000}",
                new DateOnly(2030, 12, 31),
                $"Demo Emergency Contact {number:00}",
                $"+27 00 011 {number:0000}").Value);
        }
    }

    private async Task AddVehiclesAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Vehicles.IgnoreQueryFilters().AnyAsync(
                vehicle => vehicle.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        for (var number = 1; number <= 10; number++)
        {
            dbContext.Vehicles.Add(Vehicle.Create(
                DeterministicGuid(170, number),
                DevelopmentSeedIds.TenantId,
                number <= 6
                    ? DevelopmentSeedIds.JohannesburgBranchId
                    : DevelopmentSeedIds.MpumalangaBranchId,
                $"DEMO TRK {number:00} GP",
                $"TRK-{number:000}",
                "Demo Motors",
                "Bulk Hauler",
                2022 + number % 3,
                Weight.Create(8_500m, WeightUnit.Kilograms).Value,
                Weight.Create(34_000m, WeightUnit.Kilograms).Value,
                new DateOnly(2030, 6, 30),
                new DateOnly(2030, 12, 31)).Value);
        }
    }

    private async Task AddTrailersAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Trailers.IgnoreQueryFilters().AnyAsync(
                trailer => trailer.TenantId == DevelopmentSeedIds.TenantId,
                cancellationToken))
        {
            return;
        }

        for (var number = 1; number <= 15; number++)
        {
            dbContext.Trailers.Add(Trailer.Create(
                DeterministicGuid(180, number),
                DevelopmentSeedIds.TenantId,
                number <= 9
                    ? DevelopmentSeedIds.JohannesburgBranchId
                    : DevelopmentSeedIds.MpumalangaBranchId,
                $"DEMO TRL {number:00} GP",
                $"TRL-{number:000}",
                "Demo Trailers",
                "Side Tipper",
                2021 + number % 4,
                Weight.Create(6_250m, WeightUnit.Kilograms).Value,
                Weight.Create(36_000m, WeightUnit.Kilograms).Value,
                new DateOnly(2030, 12, 31)).Value);
        }
    }

    private static Tenant CreateTenant() =>
        Tenant.Create(
            DevelopmentSeedIds.TenantId,
            "LoadIx Demo Transport",
            "LoadIx Demo Transport (Pty) Ltd",
            "DEMO-TENANT-001",
            "DEMO-TAX-000",
            "ZAR",
            "Africa/Johannesburg").Value;

    private static Address AddressAt(
        string line1,
        string city,
        string province,
        string postalCode) =>
        Address.Create(line1, null, city, province, postalCode, "ZA").Value;

    private static Guid DeterministicGuid(int group, int number) =>
        Guid.Parse($"{group:00000000}-0000-0000-0000-{number:000000000000}");
}
