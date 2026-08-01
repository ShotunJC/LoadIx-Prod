using LoadIx.Application.Edge;
using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Edge;

public sealed class CurrentEdgeDeviceRequestContext : IEdgeDeviceRequestContext
{
    public TenantId? TenantId { get; private set; }

    public Guid? EdgeDeviceId { get; private set; }

    public Guid? CredentialId { get; private set; }

    public string? Nonce { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTimeOffset? TimestampUtc { get; private set; }

    public string? ClientIpAddress { get; private set; }

    public void Set(
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid credentialId,
        string nonce,
        string idempotencyKey,
        DateTimeOffset timestampUtc,
        string? clientIpAddress)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        CredentialId = credentialId;
        Nonce = nonce;
        IdempotencyKey = idempotencyKey;
        TimestampUtc = timestampUtc;
        ClientIpAddress = clientIpAddress;
    }

    public void Clear()
    {
        TenantId = null;
        EdgeDeviceId = null;
        CredentialId = null;
        Nonce = null;
        IdempotencyKey = null;
        TimestampUtc = null;
        ClientIpAddress = null;
    }
}
