using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Instructions;

public sealed class TripGenerationOperation : ITenantOwned
{
    public Guid Id { get; init; }

    public TenantId TenantId { get; init; }

    public Guid LoadInstructionId { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}
