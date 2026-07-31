using LoadIx.Application.Security;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LoadIx.Infrastructure.Seed;

public sealed class DevelopmentIdentitySeeder(
    LoadIxDbContext dbContext,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string AdministratorEmail = "admin@loadix.example.invalid";
    public const string DriverEmail = "driver@loadix.example.invalid";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var tenantId = DevelopmentSeedIds.TenantId;
        var administratorRole = await EnsureRoleAsync(
            Guid.Parse("21000000-0000-0000-0000-000000000001"),
            tenantId,
            "Tenant Administrator",
            LoadIxPermissions.All,
            cancellationToken);
        await EnsureRoleAsync(
            Guid.Parse("21000000-0000-0000-0000-000000000002"),
            tenantId,
            "Dispatcher",
            [
                LoadIxPermissions.TenantView,
                LoadIxPermissions.UserView,
                LoadIxPermissions.CustomerView,
                LoadIxPermissions.FleetView,
                LoadIxPermissions.TripView,
                LoadIxPermissions.TripManage,
                LoadIxPermissions.TripDispatch,
                LoadIxPermissions.DocumentView,
                LoadIxPermissions.DocumentManage,
                LoadIxPermissions.ReportView,
            ],
            cancellationToken);
        await EnsureRoleAsync(
            Guid.Parse("21000000-0000-0000-0000-000000000003"),
            tenantId,
            "Read only",
            LoadIxPermissions.All.Where(permission =>
                permission.EndsWith(".View", StringComparison.Ordinal)),
            cancellationToken);
        await EnsureRoleAsync(
            Guid.Parse("21000000-0000-0000-0000-000000000004"),
            tenantId,
            "Driver",
            [
                LoadIxPermissions.TripView,
                LoadIxPermissions.TripDriverExecute,
                LoadIxPermissions.TripLocationUpdate,
                LoadIxPermissions.DocumentDriver,
            ],
            cancellationToken);

        var administrator = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == DevelopmentSeedIds.ActorId,
            cancellationToken);
        if (administrator is null)
        {
            var password = configuration["DevelopmentSeed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "DevelopmentSeed:AdminPassword is required for development seeding.");
            }

            var now = timeProvider.GetUtcNow();
            administrator = new ApplicationUser
            {
                Id = DevelopmentSeedIds.ActorId,
                TenantId = tenantId,
                UserName = AdministratorEmail,
                Email = AdministratorEmail,
                EmailConfirmed = true,
                DisplayName = "Demo Administrator",
                IsActive = true,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            var createResult = await userManager.CreateAsync(administrator, password);
            EnsureSucceeded(createResult);
        }

        if (!await userManager.IsInRoleAsync(administrator, administratorRole.Name!))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(
                administrator,
                administratorRole.Name!));
        }

        var driverRole = await dbContext.Roles.SingleAsync(
            role => role.DisplayName == "Driver",
            cancellationToken);
        var driver = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == DevelopmentSeedIds.DriverUserId,
            cancellationToken);
        if (driver is null)
        {
            var password = configuration["DevelopmentSeed:DriverPassword"]
                ?? configuration["DevelopmentSeed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "DevelopmentSeed:DriverPassword is required for development seeding.");
            }

            var now = timeProvider.GetUtcNow();
            driver = new ApplicationUser
            {
                Id = DevelopmentSeedIds.DriverUserId,
                TenantId = tenantId,
                UserName = DriverEmail,
                Email = DriverEmail,
                EmailConfirmed = true,
                DisplayName = "Demo Driver 01",
                IsActive = true,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            EnsureSucceeded(await userManager.CreateAsync(driver, password));
        }

        if (!await userManager.IsInRoleAsync(driver, driverRole.Name!))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(driver, driverRole.Name!));
        }

        var driverProfile = await dbContext.Drivers.SingleOrDefaultAsync(
            item => item.Id == DevelopmentSeedIds.DriverId,
            cancellationToken);
        if (driverProfile is not null && driverProfile.UserId != driver.Id)
        {
            driverProfile.LinkUser(driver.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationRole> EnsureRoleAsync(
        Guid id,
        Domain.Common.TenantId tenantId,
        string displayName,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (role is not null)
        {
            var desiredPermissions = permissions
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!role.GetPermissions().SetEquals(desiredPermissions))
            {
                role.SetPermissions(desiredPermissions);
                EnsureSucceeded(await roleManager.UpdateAsync(role));
            }

            return role;
        }

        role = new ApplicationRole
        {
            Id = id,
            TenantId = tenantId,
            Name = $"{tenantId.Value:N}:{displayName.Replace(' ', '-')}",
            DisplayName = displayName,
        };
        role.SetPermissions(permissions);
        EnsureSucceeded(await roleManager.CreateAsync(role));
        return role;
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
}
