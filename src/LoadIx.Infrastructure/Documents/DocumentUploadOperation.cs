using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Documents;

public sealed class DocumentUploadOperation : ITenantOwned
{
    public Guid Id { get; init; }

    public TenantId TenantId { get; init; }

    public Guid ActorUserId { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public Guid DocumentId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
