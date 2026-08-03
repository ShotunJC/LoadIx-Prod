using LoadIx.Application.Security;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Endpoints;

public static class OperationsEndpoints
{
    private const int DefaultBoardLimit = 250;
    private const int MaximumBoardLimit = 5_000;
    private const int MaximumLocationRows = 5_000;

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
            int? limit,
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
            if (delayedOnly == true)
            {
                query = query.Where(trip =>
                    trip.PlannedDeliveryAtUtc < now
                    && trip.Status != TripStatus.Completed
                    && trip.Status != TripStatus.Cancelled
                    && trip.Status != TripStatus.Rejected);
            }
            if (exceptionOnly == true)
            {
                query = query.Where(trip =>
                    trip.Status == TripStatus.ReconciliationRequired
                    || trip.Status == TripStatus.Rejected);
            }

            var take = Math.Clamp(limit ?? DefaultBoardLimit, 1, MaximumBoardLimit);
            var rows = await (
                from trip in query
                join customer in db.Customers.AsNoTracking()
                    on trip.CustomerId equals customer.Id
                join commodity in db.Commodities.AsNoTracking()
                    on trip.CommodityId equals commodity.Id
                join driverCandidate in db.Drivers.AsNoTracking()
                    on trip.DriverId equals (Guid?)driverCandidate.Id into driverJoin
                from driver in driverJoin.DefaultIfEmpty()
                join vehicleCandidate in db.Vehicles.AsNoTracking()
                    on trip.VehicleId equals (Guid?)vehicleCandidate.Id into vehicleJoin
                from vehicle in vehicleJoin.DefaultIfEmpty()
                let assignedBranchId = driver != null
                    ? (Guid?)driver.BranchId
                    : vehicle != null
                        ? (Guid?)vehicle.BranchId
                        : null
                join branchCandidate in db.Branches.AsNoTracking()
                    on assignedBranchId equals (Guid?)branchCandidate.Id into branchJoin
                from branch in branchJoin.DefaultIfEmpty()
                orderby trip.PlannedCollectionAtUtc, trip.Id
                select new OperationsBoardItem(
                    trip.Id,
                    trip.TripNumber,
                    trip.CustomerId,
                    customer.TradingName,
                    trip.CommodityId,
                    commodity.Name,
                    trip.RouteReference,
                    trip.DriverId,
                    driver == null ? null : driver.FirstName + " " + driver.LastName,
                    trip.VehicleId,
                    vehicle == null ? null : vehicle.FleetNumber,
                    branch == null ? null : (Guid?)branch.Id,
                    branch == null ? null : branch.Name,
                    trip.Status.ToString(),
                    trip.PlannedCollectionAtUtc,
                    trip.PlannedDeliveryAtUtc,
                    trip.Status == TripStatus.ReconciliationRequired
                        || trip.Status == TripStatus.Rejected,
                    trip.PlannedDeliveryAtUtc < now
                        && trip.Status != TripStatus.Completed
                        && trip.Status != TripStatus.Cancelled
                        && trip.Status != TripStatus.Rejected,
                    trip.CompletedAtUtc))
                .Take(take)
                .ToListAsync(cancellationToken);
            return Results.Ok(new OperationsBoardResponse(rows, now));
        });
        group.MapGet("/locations", async (
            LoadIxDbContext db,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var now = time.GetUtcNow().ToUniversalTime();
            var completedTodayStart = new DateTimeOffset(
                now.UtcDateTime.Date,
                TimeSpan.Zero);
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
            var visibleTrips = db.Trips.AsNoTracking()
                .Where(trip => activeStatuses.Contains(trip.Status)
                    || (trip.Status == TripStatus.Completed
                        && trip.CompletedAtUtc >= completedTodayStart
                        && trip.CompletedAtUtc < completedTodayStart.AddDays(1)));
            var latest = await db.TripLocationUpdates.AsNoTracking()
                .Where(update => visibleTrips.Select(trip => trip.Id)
                    .Contains(update.TripId))
                .GroupBy(update => update.TripId)
                .Select(grouping => grouping.OrderByDescending(update => update.CapturedAtUtc)
                    .ThenByDescending(update => update.Id).First())
                .Join(visibleTrips, update => update.TripId, trip => trip.Id,
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
                .OrderByDescending(item => item.capturedAtUtc)
                .ThenBy(item => item.tripId)
                .Take(MaximumLocationRows)
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
