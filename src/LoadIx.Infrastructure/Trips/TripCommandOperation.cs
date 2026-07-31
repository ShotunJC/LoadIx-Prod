using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Trips;

public sealed class TripCommandOperation : ITenantOwned
{
    public Guid Id { get; init; }

    public TenantId TenantId { get; init; }

    public Guid ActorUserId { get; init; }

    public Guid TripId { get; init; }

    public string Command { get; init; } = string.Empty;

    public string IdempotencyKey { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}
