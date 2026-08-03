using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Notifications;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Operations;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Trips;

public sealed class TripService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    INotificationService notificationService,
    TimeProvider timeProvider) : ITripService
{
    private const int MaximumPageSize = 100;

    private static readonly TripStatus[] TerminalStatuses =
    [
        TripStatus.Completed,
        TripStatus.Cancelled,
        TripStatus.Rejected,
    ];

    private static readonly HashSet<string> DriverCommands = new(
        [
            "ConfirmCollectionArrival",
            "StartLoading",
            "CompleteCollection",
            "DepartCollection",
            "ConfirmDeliveryArrival",
            "StartUnloading",
            "CompleteDelivery",
            "SubmitForReconciliation",
        ],
        StringComparer.Ordinal);

    public async Task<TripResult<TripPagedResponse<TripListItem>>> ListAsync(
        TripQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripPagedResponse<TripListItem>>(
            LoadIxPermissions.TripView);
        if (denied is not null) return denied;
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize
            || query.Direction is not ("asc" or "desc"))
        {
            return Fail<TripPagedResponse<TripListItem>>(
                TripFailure.Validation,
                "trips.query_invalid",
                "Page must be positive, pageSize must be between 1 and 100, and direction must be asc or desc.");
        }

        var trips = dbContext.Trips.AsNoTracking();
        if (IsDriverOnly())
        {
            var driverId = await CurrentDriverIdAsync(cancellationToken);
            if (!driverId.HasValue)
            {
                return Forbidden<TripPagedResponse<TripListItem>>(
                    "driver.profile_not_linked",
                    "Your driver account is not linked to an active driver profile.");
            }

            trips = trips.Where(item => item.DriverId == driverId.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            trips = trips.Where(item =>
                item.TripNumber.Contains(search)
                || item.ContractNumber.Contains(search)
                || item.RouteReference.Contains(search));
        }

        if (query.CustomerId.HasValue)
            trips = trips.Where(item => item.CustomerId == query.CustomerId);
        if (query.DriverId.HasValue)
            trips = trips.Where(item => item.DriverId == query.DriverId);
        if (query.VehicleId.HasValue)
            trips = trips.Where(item => item.VehicleId == query.VehicleId);
        if (query.FromUtc.HasValue)
            trips = trips.Where(item =>
                item.PlannedDeliveryAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            trips = trips.Where(item =>
                item.PlannedCollectionAtUtc <= query.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Status)
            && !string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<TripStatus>(query.Status, true, out var status))
            {
                return Fail<TripPagedResponse<TripListItem>>(
                    TripFailure.Validation,
                    "trips.status_invalid",
                    "Trip status filter is invalid.");
            }

            trips = trips.Where(item => item.Status == status);
        }

        var total = await trips.CountAsync(cancellationToken);
        var descending = query.Direction == "desc";
        trips = query.Sort switch
        {
            "tripNumber" => descending
                ? trips.OrderByDescending(item => item.TripNumber)
                    .ThenByDescending(item => item.Id)
                : trips.OrderBy(item => item.TripNumber).ThenBy(item => item.Id),
            "status" => descending
                ? trips.OrderByDescending(item => item.Status)
                    .ThenByDescending(item => item.Id)
                : trips.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "delivery" => descending
                ? trips.OrderByDescending(item => item.PlannedDeliveryAtUtc)
                    .ThenByDescending(item => item.Id)
                : trips.OrderBy(item => item.PlannedDeliveryAtUtc)
                    .ThenBy(item => item.Id),
            "collection" => descending
                ? trips.OrderByDescending(item => item.PlannedCollectionAtUtc)
                    .ThenByDescending(item => item.Id)
                : trips.OrderBy(item => item.PlannedCollectionAtUtc)
                    .ThenBy(item => item.Id),
            _ => null!,
        };
        if (trips is null)
        {
            return Fail<TripPagedResponse<TripListItem>>(
                TripFailure.Validation,
                "trips.sort_invalid",
                "Trip sort field is invalid.");
        }

        var pageQuery = trips
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
        var rows = await (
            from trip in pageQuery
            join instruction in dbContext.LoadInstructions.AsNoTracking()
                on trip.LoadInstructionId equals instruction.Id
            join customer in dbContext.Customers.AsNoTracking()
                on trip.CustomerId equals customer.Id
            join commodity in dbContext.Commodities.AsNoTracking()
                on trip.CommodityId equals commodity.Id
            join driverCandidate in dbContext.Drivers.AsNoTracking()
                on trip.DriverId equals (Guid?)driverCandidate.Id into driverJoin
            from driver in driverJoin.DefaultIfEmpty()
            join vehicleCandidate in dbContext.Vehicles.AsNoTracking()
                on trip.VehicleId equals (Guid?)vehicleCandidate.Id into vehicleJoin
            from vehicle in vehicleJoin.DefaultIfEmpty()
            select new
            {
                Trip = trip,
                InstructionNumber = instruction.InstructionNumber,
                CustomerName = customer.TradingName,
                CommodityName = commodity.Name,
                DriverName = driver == null
                    ? null
                    : driver.FirstName + " " + driver.LastName,
                VehicleReference = vehicle == null ? null : vehicle.FleetNumber,
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new TripListItem(
            row.Trip.Id,
            row.Trip.TripNumber,
            row.Trip.LoadInstructionId,
            row.InstructionNumber,
            row.Trip.CustomerId,
            row.CustomerName,
            row.Trip.RouteReference,
            row.CommodityName,
            row.Trip.PlannedQuantity,
            row.Trip.QuantityUnit.ToString(),
            row.Trip.PlannedCollectionAtUtc,
            row.Trip.PlannedDeliveryAtUtc,
            row.DriverName,
            row.VehicleReference,
            row.Trip.Status.ToString(),
            row.Trip.Status is TripStatus.ReconciliationRequired
                or TripStatus.Rejected,
            Version(row.Trip.RowVersion))).ToArray();
        return TripResult<TripPagedResponse<TripListItem>>.Success(
            new TripPagedResponse<TripListItem>(
                items,
                query.Page,
                query.PageSize,
                total,
                (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<TripResult<TripDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripDetail>(LoadIxPermissions.TripView);
        if (denied is not null) return denied;
        var trip = await TripQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is not null && IsDriverOnly()
            && !await IsAssignedDriverAsync(trip, cancellationToken))
        {
            return NotFound<TripDetail>();
        }
        return trip is null
            ? NotFound<TripDetail>()
            : TripResult<TripDetail>.Success(
                await MapAsync(trip, cancellationToken));
    }

    public async Task<TripResult<TripDetail>> CreateAsync(
        CreateTripModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var preflight = await PreflightAsync(
            model.ReservationId,
            "CreateTrip",
            idempotencyKey,
            Hash(model.ReservationId),
            cancellationToken);
        if (preflight is not null) return preflight;

        var existing = await TripQuery()
            .SingleOrDefaultAsync(
                item => item.Id == model.ReservationId,
                cancellationToken);
        if (existing is not null)
        {
            return await SaveCommandAsync(
                existing,
                "CreateTrip",
                idempotencyKey,
                Hash(model.ReservationId),
                cancellationToken);
        }

        var created = await BuildFromReservationAsync(
            model.ReservationId,
            cancellationToken);
        if (created.Failure != TripFailure.None)
            return TripResult<TripDetail>.Fail(
                created.Failure, created.Code, created.Message);
        dbContext.Trips.Add(created.Trip!);
        return await SaveCommandAsync(
            created.Trip!,
            "CreateTrip",
            idempotencyKey,
            Hash(model.ReservationId),
            cancellationToken);
    }

    public async Task<TripResult<TripDetail>> AssignAsync(
        Guid id,
        AssignTripModel model,
        bool reassign,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var command = reassign ? "ReassignTrip" : "AssignTrip";
        var hash = Hash(
            model.DriverId,
            model.VehicleId,
            string.Join(',', model.TrailerIds.Order()),
            model.Reason ?? string.Empty);
        var preflight = await PreflightAsync(
            id, command, idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (!ApplyVersion(trip, model.Version))
            return InvalidVersion<TripDetail>();

        var resourceFailure = await ValidateResourcesAsync(
            trip,
            model.DriverId,
            model.VehicleId,
            model.TrailerIds,
            cancellationToken);
        if (resourceFailure is not null) return resourceFailure;
        var requiresOverride = reassign && trip.Status != TripStatus.Assigned;
        if (requiresOverride
            && !requestContext.HasPermission(LoadIxPermissions.TripOverride))
        {
            return Forbidden<TripDetail>(
                "trip.override_permission_required",
                "Post-dispatch reassignment requires Trip.Override.");
        }

        var result = reassign
            ? trip.Reassign(
                model.DriverId,
                model.VehicleId,
                model.TrailerIds,
                Context(model.Reason, requiresOverride))
            : trip.Assign(
                model.DriverId,
                model.VehicleId,
                model.TrailerIds,
                Context(model.Reason));
        return result.IsFailure
            ? Validation<TripDetail>(result.Error!)
            : await SaveCommandAsync(
                trip, command, idempotencyKey, hash, cancellationToken);
    }

    public async Task<TripResult<TripDetail>> ExecuteAsync(
        Guid id,
        string command,
        TripCommandModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var permission = command switch
        {
            "DispatchTrip" => LoadIxPermissions.TripDispatch,
            "CompleteTrip" or "ConfirmReconciliation"
                or "ConfirmRequiredDocuments" => LoadIxPermissions.TripComplete,
            _ => LoadIxPermissions.TripManage,
        };
        var driverExecution = !requestContext.HasPermission(permission)
            && requestContext.HasPermission(LoadIxPermissions.TripDriverExecute)
            && DriverCommands.Contains(command);
        if (!driverExecution)
        {
            var denied = Authorize<TripDetail>(permission);
            if (denied is not null) return denied;
        }
        var hash = Hash(model.Reason ?? string.Empty);
        var preflight = await PreflightAsync(
            id, command, idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (driverExecution && !await IsAssignedDriverAsync(trip, cancellationToken))
        {
            return Forbidden<TripDetail>(
                "driver.trip_not_assigned",
                "Drivers can only update trips currently assigned to them.");
        }
        if (!ApplyVersion(trip, model.Version))
            return InvalidVersion<TripDetail>();
        var context = Context(model.Reason);
        var result = command switch
        {
            "DispatchTrip" => trip.Dispatch(context),
            "ConfirmCollectionArrival" => trip.ConfirmCollectionArrival(context),
            "StartLoading" => trip.StartLoading(context),
            "CompleteCollection" => trip.CompleteCollection(context),
            "DepartCollection" => trip.DepartCollection(context),
            "ConfirmDeliveryArrival" => trip.ConfirmDeliveryArrival(context),
            "StartUnloading" => trip.StartUnloading(context),
            "CompleteDelivery" => trip.CompleteDelivery(context),
            "SubmitForReconciliation" =>
                trip.SubmitForReconciliation(context),
            "CompleteTrip" => trip.Complete(context),
            "CancelTrip" => trip.Cancel(context),
            "RejectTrip" => trip.Reject(context),
            "ConfirmReconciliation" => trip.ConfirmReconciliation(),
            "ConfirmRequiredDocuments" => ConfirmDocuments(trip),
            _ => Result.Failure(new DomainError(
                "trip.command_invalid",
                "The trip command is not supported.")),
        };
        if (result.IsFailure) return Validation<TripDetail>(result.Error!);
        var saved = await SaveCommandAsync(
            trip, command, idempotencyKey, hash, cancellationToken);
        if (saved.IsSuccess && command == "CompleteTrip")
        {
            await CompleteInstructionReservationAsync(trip, cancellationToken);
        }

        return saved;
    }

    public async Task<TripResult<TripDetail>> OverrideAsync(
        Guid id,
        TripOverrideModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripDetail>(LoadIxPermissions.TripOverride);
        if (denied is not null) return denied;
        if (!Enum.TryParse<TripStatus>(model.NewStatus, true, out var newStatus))
        {
            return Fail<TripDetail>(
                TripFailure.Validation,
                "trip.status_invalid",
                "The override target status is invalid.");
        }

        var hash = Hash(model.NewStatus, model.Reason);
        var preflight = await PreflightAsync(
            id, "OverrideTrip", idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (!ApplyVersion(trip, model.Version))
            return InvalidVersion<TripDetail>();
        var previousStatus = trip.Status;
        var result = trip.ManualOverride(
            newStatus, Context(model.Reason, true));
        return result.IsFailure
            ? Validation<TripDetail>(result.Error!)
            : await SaveCommandAsync(
                trip,
                "OverrideTrip",
                idempotencyKey,
                hash,
                cancellationToken,
                new TripAuditDetails
                {
                    AdditionalEventType = "Reconciliation.Override",
                    BeforeSummaryJson = AuditSummary.Serialize(new
                    {
                        status = previousStatus.ToString(),
                    }),
                    AfterSummaryJson = AuditSummary.Serialize(new
                    {
                        status = newStatus.ToString(),
                    }),
                    Reason = model.Reason,
                });
    }

    public async Task<TripResult<TripDetail>> RecordWeighbridgeAsync(
        Guid id,
        bool source,
        TripWeighbridgeModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var driverExecution = !requestContext.HasPermission(
                LoadIxPermissions.TripManage)
            && requestContext.HasPermission(LoadIxPermissions.TripDriverExecute);
        if (!driverExecution)
        {
            var denied = Authorize<TripDetail>(LoadIxPermissions.TripManage);
            if (denied is not null) return denied;
        }
        var command = source
            ? "RecordSourceWeighbridge"
            : "RecordDestinationWeighbridge";
        var hash = Hash(
            model.GrossWeight,
            model.TareWeight,
            model.Reference,
            model.Reason ?? string.Empty);
        var preflight = await PreflightAsync(
            id, command, idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (driverExecution && !await IsAssignedDriverAsync(trip, cancellationToken))
        {
            return Forbidden<TripDetail>(
                "driver.trip_not_assigned",
                "Drivers can only update trips currently assigned to them.");
        }
        if (!ApplyVersion(trip, model.Version))
            return InvalidVersion<TripDetail>();
        var before = source
            ? new
            {
                grossWeight = trip.SourceGrossWeight,
                tareWeight = trip.SourceTareWeight,
                reference = trip.SourceWeighbridgeReference,
            }
            : new
            {
                grossWeight = trip.DestinationGrossWeight,
                tareWeight = trip.DestinationTareWeight,
                reference = trip.DestinationWeighbridgeReference,
            };
        var hasExistingMeasurement = before.grossWeight.HasValue
            || before.tareWeight.HasValue
            || !string.IsNullOrWhiteSpace(before.reference);
        var isCorrection = hasExistingMeasurement
            && (before.grossWeight != model.GrossWeight
                || before.tareWeight != model.TareWeight
                || !string.Equals(
                    before.reference,
                    model.Reference.Trim(),
                    StringComparison.Ordinal));
        if (isCorrection)
        {
            if (!requestContext.HasPermission(LoadIxPermissions.TripOverride))
            {
                return Forbidden<TripDetail>(
                    "trip.weight_correction_permission_required",
                    "Correcting a captured weight requires Trip.Override.");
            }

            if (string.IsNullOrWhiteSpace(model.Reason)
                || model.Reason.Trim().Length > 500)
            {
                return Fail<TripDetail>(
                    TripFailure.Validation,
                    "trip.weight_correction_reason_required",
                    "A correction reason of no more than 500 characters is required.");
            }
        }
        var result = source
            ? trip.RecordSourceWeighbridge(
                model.GrossWeight, model.TareWeight, model.Reference)
            : trip.RecordDestinationWeighbridge(
                model.GrossWeight, model.TareWeight, model.Reference);
        return result.IsFailure
            ? Validation<TripDetail>(result.Error!)
            : await SaveCommandAsync(
                trip,
                command,
                idempotencyKey,
                hash,
                cancellationToken,
                new TripAuditDetails
                {
                    AdditionalEventType = isCorrection
                        ? "Weight.Corrected"
                        : "Weight.Recorded",
                    BeforeSummaryJson = AuditSummary.Serialize(before),
                    AfterSummaryJson = AuditSummary.Serialize(source
                        ? new
                        {
                            grossWeight = trip.SourceGrossWeight,
                            tareWeight = trip.SourceTareWeight,
                            reference = trip.SourceWeighbridgeReference,
                        }
                        : new
                        {
                            grossWeight = trip.DestinationGrossWeight,
                            tareWeight = trip.DestinationTareWeight,
                            reference = trip.DestinationWeighbridgeReference,
                        }),
                    Reason = isCorrection ? model.Reason : null,
                });
    }

    public async Task<TripResult<TripDetail>> AddNoteAsync(
        Guid id,
        TripNoteModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var driverExecution = !requestContext.HasPermission(
                LoadIxPermissions.TripManage)
            && requestContext.HasPermission(LoadIxPermissions.TripDriverExecute);
        if (!driverExecution)
        {
            var denied = Authorize<TripDetail>(LoadIxPermissions.TripManage);
            if (denied is not null) return denied;
        }
        var hash = Hash(model.Note);
        var preflight = await PreflightAsync(
            id, "AddNote", idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (driverExecution && !await IsAssignedDriverAsync(trip, cancellationToken))
        {
            return Forbidden<TripDetail>(
                "driver.trip_not_assigned",
                "Drivers can only update trips currently assigned to them.");
        }
        if (!ApplyVersion(trip, model.Version))
            return InvalidVersion<TripDetail>();
        var result = trip.AddOperationalNote(model.Note);
        return result.IsFailure
            ? Validation<TripDetail>(result.Error!)
            : await SaveCommandAsync(
                trip, "AddNote", idempotencyKey, hash, cancellationToken);
    }

    public async Task<TripResult<TripDetail>> RecordLocationAsync(
        Guid id,
        TripLocationUpdateModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TripDetail>(LoadIxPermissions.TripLocationUpdate);
        if (denied is not null) return denied;
        var hash = Hash(
            model.Latitude,
            model.Longitude,
            model.AccuracyMetres ?? 0m,
            model.CapturedAtUtc.ToUniversalTime());
        var preflight = await PreflightAsync(
            id, "RecordLocation", idempotencyKey, hash, cancellationToken);
        if (preflight is not null) return preflight;
        var trip = await TripQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (trip is null) return NotFound<TripDetail>();
        if (!await IsAssignedDriverAsync(trip, cancellationToken))
        {
            return Forbidden<TripDetail>(
                "driver.trip_not_assigned",
                "Drivers can only submit a location for a trip assigned to them.");
        }

        if (!ApplyVersion(trip, model.Version)) return InvalidVersion<TripDetail>();
        var update = TripLocationUpdate.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            trip.Id,
            requestContext.UserId!.Value,
            model.Latitude,
            model.Longitude,
            model.AccuracyMetres,
            model.CapturedAtUtc,
            timeProvider.GetUtcNow());
        if (update.IsFailure) return Validation<TripDetail>(update.Error!);

        dbContext.TripLocationUpdates.Add(update.Value);
        return await SaveCommandAsync(
            trip, "RecordLocation", idempotencyKey, hash, cancellationToken);
    }

    internal async Task<TripBuildResult> BuildFromReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.GeneratedTrips.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == reservationId,
                cancellationToken);
        if (reservation is null)
        {
            return TripBuildResult.Fail(
                TripFailure.NotFound,
                "trip.reservation_not_found",
                "The generated trip reservation was not found.");
        }

        var instruction = await dbContext.LoadInstructions.AsNoTracking()
            .SingleAsync(
                item => item.Id == reservation.LoadInstructionId,
                cancellationToken);
        var contract = await dbContext.TransportContracts
            .Include(item => item.Routes)
            .Include(item => item.Rates)
            .AsSplitQuery()
            .SingleAsync(
                item => item.Id == instruction.TransportContractId,
                cancellationToken);
        var route = contract.Routes.Single(
            item => item.Id == instruction.ContractRouteId);
        var collectionDate = DateOnly.FromDateTime(
            instruction.CollectionWindowStartUtc.UtcDateTime);
        var rate = contract.Rates
            .Where(item => item.ContractRouteId == route.Id
                && item.EffectiveFrom <= collectionDate
                && (item.EffectiveTo == null
                    || item.EffectiveTo >= collectionDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.Id)
            .FirstOrDefault();
        if (rate is null)
        {
            return TripBuildResult.Fail(
                TripFailure.Validation,
                "trip.rate_not_effective",
                "No contract rate is effective for the planned collection date.");
        }

        var snapshot = TripRateSnapshot.Create(new ContractRateSnapshot(
            rate.Id,
            rate.RateType,
            rate.EffectiveFrom,
            rate.EffectiveTo,
            rate.FixedAmount,
            rate.BaseFee,
            rate.PerTonneAmount,
            rate.MinimumCharge,
            rate.FuelSurchargePercent,
            rate.RoundingDecimalPlaces,
            rate.RoundingMode,
            contract.DefaultCurrency));
        var created = Trip.Create(
            reservation.Id,
            reservation.TenantId,
            new TripDefinition(
                reservation.TripNumber,
                instruction.Id,
                instruction.CustomerId,
                contract.Id,
                contract.ContractNumber,
                Version(contract.RowVersion),
                route.Id,
                route.Reference,
                instruction.CommodityId,
                reservation.PlannedQuantity,
                reservation.QuantityUnit,
                instruction.CollectionWindowStartUtc,
                instruction.DeliveryWindowEndUtc),
            snapshot);
        return created.IsFailure
            ? TripBuildResult.Fail(
                TripFailure.Validation,
                created.Error!.Code,
                created.Error.Description)
            : TripBuildResult.Success(created.Value);
    }

    private async Task<TripResult<TripDetail>?> ValidateResourcesAsync(
        Trip trip,
        Guid driverId,
        Guid vehicleId,
        IReadOnlyCollection<Guid> trailerIds,
        CancellationToken cancellationToken)
    {
        if (trailerIds.Count > 3 || trailerIds.Distinct().Count() != trailerIds.Count)
        {
            return Fail<TripDetail>(
                TripFailure.Validation,
                "trip.trailers_invalid",
                "A trip supports up to three distinct trailers.");
        }

        var collectionDate = DateOnly.FromDateTime(
            trip.PlannedCollectionAtUtc.UtcDateTime);
        var driver = await dbContext.Drivers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken);
        var vehicle = await dbContext.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken);
        var trailers = await dbContext.Trailers.AsNoTracking()
            .Where(item => trailerIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (driver is null || !driver.IsActive
            || driver.LicenceExpiry < collectionDate
            || vehicle is null || !vehicle.IsActive || !vehicle.IsAvailable
            || vehicle.LicenceExpiry < collectionDate
            || vehicle.RoadworthyExpiry < collectionDate
            || trailers.Count != trailerIds.Count
            || trailers.Any(item =>
                !item.IsActive || !item.IsAvailable
                || item.LicenceExpiry < collectionDate))
        {
            return Fail<TripDetail>(
                TripFailure.Validation,
                "trip.resource_ineligible",
                "Assigned resources must be active, available, and compliant for the collection date.");
        }

        var active = dbContext.Trips.AsNoTracking()
            .Where(item => item.Id != trip.Id
                && !TerminalStatuses.Contains(item.Status)
                && item.PlannedCollectionAtUtc < trip.PlannedDeliveryAtUtc
                && item.PlannedDeliveryAtUtc > trip.PlannedCollectionAtUtc);
        var driverConflict = await active.AnyAsync(
            item => item.DriverId == driverId,
            cancellationToken);
        var vehicleConflict = await active.AnyAsync(
            item => item.VehicleId == vehicleId,
            cancellationToken);
        var trailerConflict = false;
        foreach (var trailerId in trailerIds)
        {
            var text = trailerId.ToString();
            if (await active.AnyAsync(
                item => item.TrailerIds.Contains(text),
                cancellationToken))
            {
                trailerConflict = true;
                break;
            }
        }

        return driverConflict || vehicleConflict || trailerConflict
            ? Fail<TripDetail>(
                TripFailure.Conflict,
                "trip.resource_conflict",
                "A driver, vehicle, or trailer has an overlapping active assignment.")
            : null;
    }

    private async Task<TripResult<TripDetail>?> PreflightAsync(
        Guid tripId,
        string command,
        string idempotencyKey,
        string hash,
        CancellationToken cancellationToken)
    {
        if (!ValidIdempotencyKey(idempotencyKey))
        {
            return Fail<TripDetail>(
                TripFailure.Validation,
                "idempotency.key_invalid",
                "Idempotency-Key must contain 8 to 128 letters, digits, dots, colons, underscores, or hyphens.");
        }

        var operation = await dbContext.TripCommandOperations.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ActorUserId == requestContext.UserId
                    && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (operation is null) return null;
        if (operation.TripId != tripId
            || operation.Command != command
            || operation.RequestHash != hash)
        {
            return Fail<TripDetail>(
                TripFailure.Conflict,
                "idempotency.payload_mismatch",
                "The idempotency key was already used for a different trip command.");
        }

        var replay = await TripQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tripId, cancellationToken);
        return replay is null
            ? NotFound<TripDetail>()
            : TripResult<TripDetail>.Success(
                await MapAsync(replay, cancellationToken));
    }

    private async Task<TripResult<TripDetail>> SaveCommandAsync(
        Trip trip,
        string command,
        string idempotencyKey,
        string hash,
        CancellationToken cancellationToken,
        TripAuditDetails? auditDetails = null)
    {
        dbContext.TripCommandOperations.Add(new TripCommandOperation
        {
            Id = Guid.NewGuid(),
            TenantId = requestContext.TenantId!.Value,
            ActorUserId = requestContext.UserId!.Value,
            TripId = trip.Id,
            Command = command,
            IdempotencyKey = idempotencyKey,
            RequestHash = hash,
            CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
        });
        return await SaveAsync(trip, command, cancellationToken, auditDetails);
    }

    private async Task<TripResult<TripDetail>> SaveAsync(
        Trip trip,
        string action,
        CancellationToken cancellationToken,
        TripAuditDetails? auditDetails = null)
    {
        dbContext.TripTransitions.AddRange(
            trip.Transitions.Where(item => item.TripId == Guid.Empty));
        dbContext.TripAssignments.AddRange(
            trip.Assignments.Where(item => item.TripId == Guid.Empty));
        dbContext.OperationalOutboxMessages.Add(new OperationalOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = trip.TenantId,
            TripId = trip.Id,
            EventType = EventTypeFor(action, trip),
            Payload = JsonSerializer.Serialize(new
            {
                tripId = trip.Id,
                tripNumber = trip.TripNumber,
                status = trip.Status.ToString(),
                action,
                driverId = trip.DriverId,
                vehicleId = trip.VehicleId,
                occurredAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
            }),
            OccurredAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
        });
        await QueueTripNotificationAsync(trip, action, cancellationToken);
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await auditWriter.WriteAsync(
                        $"Trip.{action}",
                        "Succeeded",
                        "Trip",
                        trip.Id.ToString(),
                        requestContext.TenantId,
                        requestContext.UserId,
                        requestContext.IsSupportContext,
                        null,
                        cancellationToken,
                        beforeSummaryJson: auditDetails?.BeforeSummaryJson,
                        afterSummaryJson: auditDetails?.AfterSummaryJson
                            ?? AuditSummary.Serialize(new
                            {
                                status = trip.Status.ToString(),
                                driverId = trip.DriverId,
                                vehicleId = trip.VehicleId,
                                transition = trip.Transitions.LastOrDefault() is { } transition
                                    ? new
                                    {
                                        previousStatus = transition.PreviousStatus.ToString(),
                                        newStatus = transition.NewStatus.ToString(),
                                        transition.Reason,
                                        transition.IsManualOverride,
                                    }
                                    : null,
                            }),
                        reason: auditDetails?.Reason);
                    var latestTransition = trip.Transitions.LastOrDefault();
                    if (latestTransition is not null)
                    {
                        await auditWriter.WriteAsync(
                            "Trip.Transitioned",
                            "Succeeded",
                            "Trip",
                            trip.Id.ToString(),
                            requestContext.TenantId,
                            requestContext.UserId,
                            requestContext.IsSupportContext,
                            null,
                            cancellationToken,
                            beforeSummaryJson: AuditSummary.Serialize(new
                            {
                                status = latestTransition.PreviousStatus.ToString(),
                            }),
                            afterSummaryJson: AuditSummary.Serialize(new
                            {
                                status = latestTransition.NewStatus.ToString(),
                                isManualOverride = latestTransition.IsManualOverride,
                            }),
                            reason: latestTransition.Reason);
                    }
                    if (!string.IsNullOrWhiteSpace(auditDetails?.AdditionalEventType))
                    {
                        await auditWriter.WriteAsync(
                            auditDetails.AdditionalEventType,
                            "Succeeded",
                            "Trip",
                            trip.Id.ToString(),
                            requestContext.TenantId,
                            requestContext.UserId,
                            requestContext.IsSupportContext,
                            null,
                            cancellationToken,
                            beforeSummaryJson: auditDetails.BeforeSummaryJson,
                            afterSummaryJson: auditDetails.AfterSummaryJson,
                            reason: auditDetails.Reason);
                    }
                    await transaction.CommitAsync(cancellationToken);
                    return TripResult<TripDetail>.Success(
                        await MapAsync(trip, cancellationToken));
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Fail<TripDetail>(
                        TripFailure.Conflict,
                        "concurrency.conflict",
                        "The trip changed after it was loaded. Refresh and try again.");
                }
                catch (DbUpdateException exception) when (
                    exception.InnerException?.Message.Contains(
                        "duplicate", StringComparison.OrdinalIgnoreCase) == true
                    || exception.InnerException?.Message.Contains(
                        "UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Fail<TripDetail>(
                        TripFailure.Conflict,
                        "trip.duplicate",
                        "The trip number or idempotency key already exists.");
                }
            });
    }

    private static string EventTypeFor(string action, Trip trip) => action switch
    {
        "CreateTrip" => "trip.created",
        "AssignTrip" or "ReassignTrip" => "trip.assignmentChanged",
        "RecordSourceWeighbridge" or "RecordDestinationWeighbridge" => "trip.weighbridgeCaptured",
        "RecordLocation" => "trip.driverArrival",
        "CompleteTrip" => "trip.completed",
        "DispatchTrip" => "trip.statusChanged",
        "ConfirmCollectionArrival" => "trip.driverArrival",
        "ConfirmDeliveryArrival" => "trip.driverArrival",
        _ when trip.Status == TripStatus.ReconciliationRequired => "trip.reconciliationException",
        _ when trip.Status == TripStatus.Completed => "trip.completed",
        _ => "trip.statusChanged",
    };

    private sealed record TripAuditDetails
    {
        public string? AdditionalEventType { get; init; }

        public string? BeforeSummaryJson { get; init; }

        public string? AfterSummaryJson { get; init; }

        public string? Reason { get; init; }
    }

    private async Task QueueTripNotificationAsync(
        Trip trip,
        string action,
        CancellationToken cancellationToken)
    {
        if (!trip.DriverId.HasValue) return;
        var driver = await dbContext.Drivers.AsNoTracking()
            .Where(item => item.Id == trip.DriverId.Value && item.UserId.HasValue)
            .Select(item => new { item.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (driver?.UserId is null) return;

        var type = action switch
        {
            "AssignTrip" or "ReassignTrip" => NotificationTypes.TripAssignment,
            "DispatchTrip" => NotificationTypes.Dispatch,
            "ConfirmCollectionArrival" => NotificationTypes.CollectionArrival,
            "ConfirmDeliveryArrival" => NotificationTypes.DeliveryArrival,
            "SubmitForReconciliation" => NotificationTypes.ReconciliationException,
            "CompleteTrip" => NotificationTypes.TripCompletion,
            _ when trip.Status == TripStatus.ReconciliationRequired => NotificationTypes.ReconciliationException,
            _ => null,
        };
        if (type is null) return;

        var title = type switch
        {
            NotificationTypes.TripAssignment => $"Trip {trip.TripNumber} assigned",
            NotificationTypes.Dispatch => $"Trip {trip.TripNumber} dispatched",
            NotificationTypes.CollectionArrival => $"Collection arrival recorded for {trip.TripNumber}",
            NotificationTypes.DeliveryArrival => $"Delivery arrival recorded for {trip.TripNumber}",
            NotificationTypes.ReconciliationException => $"Trip {trip.TripNumber} needs reconciliation",
            NotificationTypes.TripCompletion => $"Trip {trip.TripNumber} completed",
            _ => $"Trip {trip.TripNumber} update",
        };
        var body = $"{trip.TripNumber} is now {trip.Status} on route {trip.RouteReference}.";
        await notificationService.QueueAsync(
            new NotificationRequest(
                driver.UserId.Value,
                type,
                title,
                body,
                $"trip:{trip.Id:N}:{type}:{trip.Status}",
                ["in-app", "email", "sms"]),
            cancellationToken);
    }

    private async Task CompleteInstructionReservationAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var instruction = await dbContext.LoadInstructions
            .Include(item => item.Trips)
            .SingleAsync(
                item => item.Id == trip.LoadInstructionId,
                cancellationToken);
        var completedQuantity = trip.DestinationNetWeight
            ?? trip.SourceNetWeight
            ?? trip.PlannedQuantity;
        var result = instruction.CompleteTrip(trip.Id, completedQuantity);
        if (result.IsSuccess)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<TripDetail> MapAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var instructionNumber = await dbContext.LoadInstructions.AsNoTracking()
            .Where(item => item.Id == trip.LoadInstructionId)
            .Select(item => item.InstructionNumber)
            .SingleAsync(cancellationToken);
        var customerName = await dbContext.Customers.AsNoTracking()
            .Where(item => item.Id == trip.CustomerId)
            .Select(item => item.TradingName)
            .SingleAsync(cancellationToken);
        var route = await dbContext.ContractRoutes.AsNoTracking()
            .Where(item => item.Id == trip.ContractRouteId)
            .Select(item => new
            {
                item.SourceLocationId,
                item.DestinationLocationId,
            })
            .SingleAsync(cancellationToken);
        var locations = await dbContext.Locations.AsNoTracking()
            .Where(item => item.Id == route.SourceLocationId
                || item.Id == route.DestinationLocationId)
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var commodityName = await dbContext.Commodities.AsNoTracking()
            .Where(item => item.Id == trip.CommodityId)
            .Select(item => item.Name)
            .SingleAsync(cancellationToken);
        var driverNames = await dbContext.Drivers.AsNoTracking()
            .ToDictionaryAsync(
                item => item.Id,
                item => item.FirstName + " " + item.LastName,
                cancellationToken);
        var vehicleNames = await dbContext.Vehicles.AsNoTracking()
            .ToDictionaryAsync(
                item => item.Id,
                item => item.FleetNumber + " · " + item.Registration,
                cancellationToken);
        var trailerNames = await dbContext.Trailers.AsNoTracking()
            .ToDictionaryAsync(
                item => item.Id,
                item => item.FleetNumber + " · " + item.Registration,
                cancellationToken);
        var currentTrailerIds = trip.CurrentTrailerIds;
        return new TripDetail(
            trip.Id,
            trip.TripNumber,
            trip.LoadInstructionId,
            instructionNumber,
            trip.CustomerId,
            customerName,
            trip.TransportContractId,
            trip.ContractNumber,
            trip.ContractVersion,
            trip.ContractRouteId,
            trip.RouteReference,
            locations[route.SourceLocationId],
            locations[route.DestinationLocationId],
            trip.CommodityId,
            commodityName,
            trip.PlannedQuantity,
            trip.QuantityUnit.ToString(),
            new TripRateSnapshotModel(
                trip.RateSnapshot.RateId,
                trip.RateSnapshot.RateType.ToString(),
                trip.RateSnapshot.EffectiveFrom,
                trip.RateSnapshot.EffectiveTo,
                trip.RateSnapshot.FixedAmount,
                trip.RateSnapshot.BaseFee,
                trip.RateSnapshot.PerTonneAmount,
                trip.RateSnapshot.MinimumCharge,
                trip.RateSnapshot.FuelSurchargePercent,
                trip.RateSnapshot.RoundingDecimalPlaces,
                trip.RateSnapshot.RoundingMode.ToString(),
                trip.RateSnapshot.CurrencyCode),
            trip.DriverId,
            trip.DriverId.HasValue
                ? driverNames.GetValueOrDefault(trip.DriverId.Value)
                : null,
            trip.VehicleId,
            trip.VehicleId.HasValue
                ? vehicleNames.GetValueOrDefault(trip.VehicleId.Value)
                : null,
            currentTrailerIds,
            currentTrailerIds.Select(id =>
                trailerNames.GetValueOrDefault(id, id.ToString())).ToArray(),
            trip.PlannedCollectionAtUtc,
            trip.PlannedDeliveryAtUtc,
            trip.Status.ToString(),
            trip.OperationalNotes,
            trip.CancellationOrRejectionReason,
            trip.SourceGrossWeight,
            trip.SourceTareWeight,
            trip.SourceNetWeight,
            trip.SourceWeighbridgeReference,
            trip.DestinationGrossWeight,
            trip.DestinationTareWeight,
            trip.DestinationNetWeight,
            trip.DestinationWeighbridgeReference,
            trip.IsReconciled,
            trip.HasRequiredDocuments,
            trip.Transitions.OrderBy(item => item.OccurredAtUtc)
                .ThenBy(item => item.Id)
                .Select(item => new TripTransitionModel(
                    item.Id,
                    item.PreviousStatus.ToString(),
                    item.NewStatus.ToString(),
                    item.ActorUserId,
                    item.OccurredAtUtc,
                    item.Reason,
                    item.IsManualOverride))
                .ToArray(),
            trip.Assignments.OrderByDescending(item => item.AssignedAtUtc)
                .ThenByDescending(item => item.Id)
                .Select(item =>
                {
                    var ids = ParseIds(item.TrailerIds);
                    return new TripAssignmentModel(
                        item.Id,
                        item.DriverId,
                        driverNames.GetValueOrDefault(
                            item.DriverId, item.DriverId.ToString()),
                        item.VehicleId,
                        vehicleNames.GetValueOrDefault(
                            item.VehicleId, item.VehicleId.ToString()),
                        ids,
                        ids.Select(id => trailerNames.GetValueOrDefault(
                            id, id.ToString())).ToArray(),
                        item.ActorUserId,
                        item.AssignedAtUtc,
                        item.EndedAtUtc,
                        item.Reason);
                })
                .ToArray(),
            Version(trip.RowVersion));
    }

    private IQueryable<Trip> TripQuery() =>
        dbContext.Trips
            .Include(item => item.Transitions)
            .Include(item => item.Assignments)
            .AsSplitQuery();

    private bool IsDriverOnly() =>
        requestContext.HasPermission(LoadIxPermissions.TripDriverExecute)
        && !requestContext.HasPermission(LoadIxPermissions.TripManage);

    private async Task<Guid?> CurrentDriverIdAsync(
        CancellationToken cancellationToken) =>
        !requestContext.UserId.HasValue
            ? null
            : await dbContext.Drivers.AsNoTracking()
                .Where(driver => driver.UserId == requestContext.UserId
                    && driver.IsActive)
                .Select(driver => (Guid?)driver.Id)
                .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> IsAssignedDriverAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var driverId = await CurrentDriverIdAsync(cancellationToken);
        return driverId.HasValue && trip.DriverId == driverId.Value;
    }

    private TripCommandContext Context(
        string? reason = null,
        bool isManualOverride = false) =>
        new(
            requestContext.UserId!.Value,
            timeProvider.GetUtcNow().ToUniversalTime(),
            reason,
            isManualOverride);

    private bool ApplyVersion(Trip trip, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            dbContext.Entry(trip).Property("RowVersion")
                .OriginalValue = Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private TripResult<T>? Authorize<T>(string permission) =>
        !requestContext.TenantId.HasValue
        || !requestContext.UserId.HasValue
        || !requestContext.HasPermission(permission)
            ? Forbidden<T>(
                "authorization.denied",
                "You do not have permission to perform this operation.")
            : null;

    private static Result ConfirmDocuments(Trip trip)
    {
        trip.ConfirmRequiredDocuments();
        return Result.Success();
    }

    private static Guid[] ParseIds(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToArray();

    private static bool ValidIdempotencyKey(string key) =>
        key.Length is >= 8 and <= 128
        && key.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '.' or ':' or '_' or '-');

    private static string Hash(params object[] values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(
                '|',
                values.Select(value => Convert.ToString(
                    value, CultureInfo.InvariantCulture))))));

    private static string Version(byte[] value) => Convert.ToBase64String(value);

    private static TripResult<T> NotFound<T>() =>
        Fail<T>(
            TripFailure.NotFound,
            "trip.not_found",
            "The requested trip was not found.");

    private static TripResult<T> InvalidVersion<T>() =>
        Fail<T>(
            TripFailure.Validation,
            "concurrency.version_required",
            "A valid version is required.");

    private static TripResult<T> Validation<T>(DomainError error) =>
        Fail<T>(TripFailure.Validation, error.Code, error.Description);

    private static TripResult<T> Forbidden<T>(string code, string message) =>
        Fail<T>(TripFailure.Forbidden, code, message);

    private static TripResult<T> Fail<T>(
        TripFailure failure,
        string code,
        string message) =>
        TripResult<T>.Fail(failure, code, message);

    internal sealed record TripBuildResult(
        Trip? Trip,
        TripFailure Failure,
        string Code,
        string Message)
    {
        public static TripBuildResult Success(Trip trip) =>
            new(trip, TripFailure.None, string.Empty, string.Empty);

        public static TripBuildResult Fail(
            TripFailure failure,
            string code,
            string message) =>
            new(null, failure, code, message);
    }
}
