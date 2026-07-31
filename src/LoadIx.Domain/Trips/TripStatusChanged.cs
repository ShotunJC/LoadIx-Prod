using LoadIx.Domain.Common;

namespace LoadIx.Domain.Trips;

public sealed record TripStatusChanged(
    Guid TripId,
    TenantId TenantId,
    TripStatus PreviousStatus,
    TripStatus NewStatus,
    Guid ActorUserId,
    bool IsManualOverride,
    DateTimeOffset TimestampUtc)
    : DomainEvent(TimestampUtc);
