using LoadIx.Domain.Common;

namespace LoadIx.Application.Edge;

public interface IEdgeDeviceRequestContext
{
    TenantId? TenantId { get; }

    Guid? EdgeDeviceId { get; }

    Guid? CredentialId { get; }

    string? Nonce { get; }

    string? IdempotencyKey { get; }
}
