using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class EdgeDeviceCredential : Entity<Guid>, ITenantOwned
{
    private EdgeDeviceCredential(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        EdgeDeviceCredentialType type,
        string secretHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc,
        Guid? replacedCredentialId)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        Type = type;
        SecretHash = secretHash;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
        ReplacedCredentialId = replacedCredentialId;
    }

    private EdgeDeviceCredential()
    {
        SecretHash = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public EdgeDeviceCredentialType Type { get; private set; }

    public string SecretHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public Guid? ReplacedCredentialId { get; private set; }

    public static Result<EdgeDeviceCredential> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        EdgeDeviceCredentialType type,
        string secretHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null,
        Guid? replacedCredentialId = null)
    {
        var error = string.IsNullOrWhiteSpace(secretHash)
            ? new DomainError(
                "edge_credential.secret_hash_required",
                "A hashed credential secret is required.")
            : null;
        if (id == Guid.Empty || tenantId.IsEmpty || edgeDeviceId == Guid.Empty)
        {
            error = new DomainError(
                "edge_credential.identifiers_required",
                "Credential, tenant, and edge device identifiers are required.");
        }
        else if (!Enum.IsDefined(type))
        {
            error = new DomainError(
                "edge_credential.type_invalid",
                "The credential type is not supported.");
        }
        else if (expiresAtUtc.HasValue
            && expiresAtUtc.Value <= createdAtUtc)
        {
            error = new DomainError(
                "edge_credential.expiry_invalid",
                "Credential expiry must be after creation.");
        }

        return error is not null
            ? Result<EdgeDeviceCredential>.Failure(error)
            : Result<EdgeDeviceCredential>.Success(new EdgeDeviceCredential(
                id,
                tenantId,
                edgeDeviceId,
                type,
                secretHash.Trim(),
                createdAtUtc,
                expiresAtUtc,
                replacedCredentialId));
    }

    public bool IsUsableAt(DateTimeOffset nowUtc) =>
        RevokedAtUtc is null
        && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > nowUtc);

    public void MarkUsed(DateTimeOffset usedAtUtc) =>
        LastUsedAtUtc = usedAtUtc.ToUniversalTime();

    public void Revoke(DateTimeOffset revokedAtUtc) =>
        RevokedAtUtc ??= revokedAtUtc.ToUniversalTime();
}
