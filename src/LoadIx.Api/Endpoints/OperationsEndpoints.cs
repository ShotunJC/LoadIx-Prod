using LoadIx.Application.Security;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Endpoints;

public static class OperationsEndpoints
{
    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/operations").WithTags("Operations control tower")
            .RequireAuthorization(LoadIxPermissions.TripView);
        group.MapGet("/board", async (
            string? search,
            Guid? customerId,
            Guid? branchId,
            Guid? driverId,
            Guid? vehicleId,
            Guid? commodityId,
            bool? delayedOnly,
            bool? exceptionOnly,
            LoadIxDbContext db,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var now = time.GetUtcNow().ToUniversalTime();
            var completedTodayStart = new DateTimeOffset(
                now.UtcDateTime.Date,
                TimeSpan.Zero);
            var completedTodayEnd = completedTodayStart.AddDays(1);
            var activeStatuses = new[]
            {
                TripStatus.Planned,
                TripStatus.Assigned,
                TripStatus.Dispatched,
                TripStatus.AtCollection,
                TripStatus.Loading,
                TripStatus.CollectionComplete,
                TripStatus.InTransit,
                TripStatus.AtDelivery,
                TripStatus.Unloading,
                TripStatus.DeliveryComplete,
                TripStatus.ReconciliationRequired,
            };
            var query = db.Trips.AsNoTracking()
                .Where(trip => activeStatuses.Contains(trip.Status)
                    || (trip.Status == TripStatus.Completed
                        && trip.CompletedAtUtc.HasValue
                        && trip.CompletedAtUtc.Value >= completedTodayStart
                        && trip.CompletedAtUtc.Value < completedTodayEnd));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(trip => trip.TripNumber.Contains(value)
                    || trip.RouteReference.Contains(value)
                    || trip.ContractNumber.Contains(value));
            }
            if (customerId.HasValue) query = query.Where(trip => trip.CustomerId == customerId);
            if (driverId.HasValue) query = query.Where(trip => trip.DriverId == driverId);
            if (vehicleId.HasValue) query = query.Where(trip => trip.VehicleId == vehicleId);
            if (commodityId.HasValue) query = query.Where(trip => trip.CommodityId == commodityId);
            if (branchId.HasValue)
            {
                query = query.Where(trip =>
                    db.Drivers.Any(driver => driver.Id == trip.DriverId && driver.BranchId == branchId)
                    || db.Vehicles.Any(vehicle => vehicle.Id == trip.VehicleId && vehicle.BranchId == branchId));
            }

            var rows = await query
                .OrderBy(trip => trip.PlannedCollectionAtUtc)
                .ThenBy(trip => trip.Id)
                .Select(trip => new OperationsBoardItem(
                    trip.Id,
                    trip.TripNumber,
                    trip.CustomerId,
                    db.Customers.Where(customer => customer.Id == trip.CustomerId)
                        .Select(customer => customer.TradingName).Single(),
                    trip.CommodityId,
                    db.Commodities.Where(commodity => commodity.Id == trip.CommodityId)
                        .Select(commodity => commodity.Name).Single(),
                    trip.RouteReference,
                    trip.DriverId,
                    db.Drivers.Where(driver => driver.Id == trip.DriverId)
                        .Select(driver => driver.DisplayName).SingleOrDefault(),
                    trip.VehicleId,
                    db.Vehicles.Where(vehicle => vehicle.Id == trip.VehicleId)
                        .Select(vehicle => vehicle.FleetNumber).SingleOrDefault(),
                    db.Branches.Where(branch =>
                            db.Drivers.Any(driver => driver.Id == trip.DriverId && driver.BranchId == branch.Id)
                            || db.Vehicles.Any(vehicle => vehicle.Id == trip.VehicleId && vehicle.BranchId == branch.Id))
                        .Select(branch => (Guid?)branch.Id).FirstOrDefault(),
                    db.Branches.Where(branch =>
                            db.Drivers.Any(driver => driver.Id == trip.DriverId && driver.BranchId == branch.Id)
                            || db.Vehicles.Any(vehicle => vehicle.Id == trip.VehicleId && vehicle.BranchId == branch.Id))
                        .Select(branch => branch.Name).FirstOrDefault(),
                    trip.Status.ToString(),
                    trip.PlannedCollectionAtUtc,
                    trip.PlannedDeliveryAtUtc,
                    trip.Status == TripStatus.ReconciliationRequired || trip.Status == TripStatus.Rejected,
                    trip.PlannedDeliveryAtUtc < now
                        && trip.Status != TripStatus.Completed
                        && trip.Status != TripStatus.Cancelled
                        && trip.Status != TripStatus.Rejected,
                    trip.CompletedAtUtc))
                .Take(250)
                .ToListAsync(cancellationToken);
            if (delayedOnly == true) rows = rows.Where(item => item.IsDelayed).ToList();
            if (exceptionOnly == true) rows = rows.Where(item => item.HasException).ToList();
            return Results.Ok(new OperationsBoardResponse(rows, now));
        });
        group.MapGet("/locations", async (LoadIxDbContext db, CancellationToken cancellationToken) =>
        {
            var latest = await db.TripLocationUpdates.AsNoTracking()
                .GroupBy(update => update.TripId)
                .Select(grouping => grouping.OrderByDescending(update => update.CapturedAtUtc)
                    .ThenByDescending(update => update.Id).First())
                .Join(db.Trips.AsNoTracking(), update => update.TripId, trip => trip.Id,
                    (update, trip) => new
                    {
                        tripId = trip.Id,
                        tripNumber = trip.TripNumber,
                        status = trip.Status.ToString(),
                        latitude = update.Latitude,
                        longitude = update.Longitude,
                        accuracyMetres = update.AccuracyMetres,
                        capturedAtUtc = update.CapturedAtUtc,
                    })
                .ToListAsync(cancellationToken);
            return Results.Ok(latest);
        });
        return group;
    }
}

public sealed record OperationsBoardResponse(
    IReadOnlyCollection<OperationsBoardItem> Items,
    DateTimeOffset LastUpdatedAtUtc);

public sealed record OperationsBoardItem(
    Guid TripId,
    string TripNumber,
    Guid CustomerId,
    string CustomerName,
    Guid CommodityId,
    string CommodityName,
    string RouteReference,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleReference,
    Guid? BranchId,
    string? BranchName,
    string Status,
    DateTimeOffset PlannedCollectionAtUtc,
    DateTimeOffset PlannedDeliveryAtUtc,
    bool HasException,
    bool IsDelayed,
    DateTimeOffset? CompletedAtUtc);
