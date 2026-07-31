using LoadIx.Domain.Common;

namespace LoadIx.Domain.Trips;

public sealed class TripAssignment : Entity<Guid>, ITenantOwned
{
    private TripAssignment(
        Guid id,
        TenantId tenantId,
        Guid driverId,
        Guid vehicleId,
        string trailerIds,
        Guid actorUserId,
        DateTimeOffset assignedAtUtc,
        string? reason)
        : base(id)
    {
        TenantId = tenantId;
        DriverId = driverId;
        VehicleId = vehicleId;
        TrailerIds = trailerIds;
        ActorUserId = actorUserId;
        AssignedAtUtc = assignedAtUtc;
        Reason = reason;
    }

    private TripAssignment()
    {
        TrailerIds = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public Guid DriverId { get; private set; }

    public Guid VehicleId { get; private set; }

    public string TrailerIds { get; private set; }

    public Guid ActorUserId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public string? Reason { get; private set; }

    internal static TripAssignment Create(
        TenantId tenantId,
        Guid driverId,
        Guid vehicleId,
        IReadOnlyCollection<Guid> trailerIds,
        Guid actorUserId,
        DateTimeOffset assignedAtUtc,
        string? reason) =>
        new(
            Guid.NewGuid(),
            tenantId,
            driverId,
            vehicleId,
            string.Join(',', trailerIds.Order()),
            actorUserId,
            assignedAtUtc.ToUniversalTime(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());

    internal void End(DateTimeOffset endedAtUtc) =>
        EndedAtUtc = endedAtUtc.ToUniversalTime();
}
