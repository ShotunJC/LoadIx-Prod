using LoadIx.Domain.Common;

namespace LoadIx.Domain.Trips;

public sealed class TripTransition : Entity<Guid>, ITenantOwned
{
    private TripTransition(
        Guid id,
        TenantId tenantId,
        TripStatus previousStatus,
        TripStatus newStatus,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        string? reason,
        bool isManualOverride)
        : base(id)
    {
        TenantId = tenantId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ActorUserId = actorUserId;
        OccurredAtUtc = occurredAtUtc;
        Reason = reason;
        IsManualOverride = isManualOverride;
    }

    private TripTransition()
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid TripId { get; private set; }

    public TripStatus PreviousStatus { get; private set; }

    public TripStatus NewStatus { get; private set; }

    public Guid ActorUserId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Reason { get; private set; }

    public bool IsManualOverride { get; private set; }

    internal static TripTransition Create(
        TenantId tenantId,
        TripStatus previousStatus,
        TripStatus newStatus,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        string? reason,
        bool isManualOverride) =>
        new(
            Guid.NewGuid(),
            tenantId,
            previousStatus,
            newStatus,
            actorUserId,
            occurredAtUtc.ToUniversalTime(),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            isManualOverride);
}
