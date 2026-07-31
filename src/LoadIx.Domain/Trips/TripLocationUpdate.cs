using LoadIx.Domain.Common;

namespace LoadIx.Domain.Trips;

public sealed class TripLocationUpdate : Entity<Guid>, ITenantOwned
{
    private TripLocationUpdate(
        Guid id,
        TenantId tenantId,
        Guid tripId,
        Guid driverUserId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMetres,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        TripId = tripId;
        DriverUserId = driverUserId;
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMetres = accuracyMetres;
        CapturedAtUtc = capturedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
    }

    private TripLocationUpdate()
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public Guid DriverUserId { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public decimal? AccuracyMetres { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public static Result<TripLocationUpdate> Create(
        Guid id,
        TenantId tenantId,
        Guid tripId,
        Guid driverUserId,
        decimal latitude,
        decimal longitude,
        decimal? accuracyMetres,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || tripId == Guid.Empty
            || driverUserId == Guid.Empty)
        {
            return Result<TripLocationUpdate>.Failure(new DomainError(
                "trip.location.identifiers_required",
                "Trip, tenant, driver, and location identifiers are required."));
        }

        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return Result<TripLocationUpdate>.Failure(new DomainError(
                "trip.location.coordinates_invalid",
                "Latitude or longitude is outside its valid range."));
        }

        if (accuracyMetres is <= 0m or > 100_000m)
        {
            return Result<TripLocationUpdate>.Failure(new DomainError(
                "trip.location.accuracy_invalid",
                "Location accuracy must be between zero and 100 kilometres."));
        }

        return Result<TripLocationUpdate>.Success(new TripLocationUpdate(
            id,
            tenantId,
            tripId,
            driverUserId,
            latitude,
            longitude,
            accuracyMetres,
            capturedAtUtc.ToUniversalTime(),
            receivedAtUtc.ToUniversalTime()));
    }
}
