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
        Guid? replacedCredentialId,
        DateTimeOffset activatedAtUtc,
        DateTimeOffset? confirmedAtUtc,
        DateTimeOffset? overlapExpiresAtUtc,
        EdgeDeviceCredentialStatus status)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        Type = type;
        SecretHash = secretHash;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
        ReplacedCredentialId = replacedCredentialId;
        ActivatedAtUtc = activatedAtUtc.ToUniversalTime();
        ConfirmedAtUtc = confirmedAtUtc?.ToUniversalTime();
        OverlapExpiresAtUtc = overlapExpiresAtUtc?.ToUniversalTime();
        Status = status;
    }

    private EdgeDeviceCredential()
    {
        SecretHash = string.Empty;
        Status = EdgeDeviceCredentialStatus.Active;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public EdgeDeviceCredentialType Type { get; private set; }

    public string SecretHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ActivatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public DateTimeOffset? OverlapExpiresAtUtc { get; private set; }

    public EdgeDeviceCredentialStatus Status { get; private set; }

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
        Guid? replacedCredentialId = null,
        DateTimeOffset? activatedAtUtc = null,
        DateTimeOffset? confirmedAtUtc = null,
        DateTimeOffset? overlapExpiresAtUtc = null,
        EdgeDeviceCredentialStatus status = EdgeDeviceCredentialStatus.Active)
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
        else if (activatedAtUtc.HasValue
            && activatedAtUtc.Value < createdAtUtc)
        {
            error = new DomainError(
                "edge_credential.activation_invalid",
                "Credential activation cannot precede creation.");
        }
        else if (overlapExpiresAtUtc.HasValue
            && overlapExpiresAtUtc.Value <= createdAtUtc)
        {
            error = new DomainError(
                "edge_credential.overlap_expiry_invalid",
                "Credential overlap expiry must be after creation.");
        }
        else if (!Enum.IsDefined(status))
        {
            error = new DomainError(
                "edge_credential.status_invalid",
                "The credential lifecycle status is not supported.");
        }
        else if (status == EdgeDeviceCredentialStatus.PendingConfirmation
            && (!replacedCredentialId.HasValue || !overlapExpiresAtUtc.HasValue))
        {
            error = new DomainError(
                "edge_credential.confirmation_details_required",
                "A pending replacement credential requires a previous credential and overlap expiry.");
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
                replacedCredentialId,
                activatedAtUtc ?? createdAtUtc,
                confirmedAtUtc ?? (status == EdgeDeviceCredentialStatus.Active
                    ? activatedAtUtc ?? createdAtUtc
                    : null),
                overlapExpiresAtUtc,
                status));
    }

    public bool IsUsableAt(DateTimeOffset nowUtc) =>
        RevokedAtUtc is null
        && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > nowUtc)
        && (Status != EdgeDeviceCredentialStatus.PendingConfirmation
            || !OverlapExpiresAtUtc.HasValue
            || OverlapExpiresAtUtc.Value > nowUtc);

    public void MarkUsed(DateTimeOffset usedAtUtc) =>
        LastUsedAtUtc = usedAtUtc.ToUniversalTime();

    public void Revoke(DateTimeOffset revokedAtUtc) =>
        RevokedAtUtc ??= revokedAtUtc.ToUniversalTime();

    public Result Confirm(DateTimeOffset confirmedAtUtc)
    {
        if (Status != EdgeDeviceCredentialStatus.PendingConfirmation)
        {
            return Result.Failure(new DomainError(
                "edge_credential.not_pending_confirmation",
                "The credential is not awaiting confirmation."));
        }

        var confirmed = confirmedAtUtc.ToUniversalTime();
        if (!IsUsableAt(confirmed))
        {
            return Result.Failure(new DomainError(
                "edge_credential.confirmation_expired",
                "The credential confirmation window has expired."));
        }

        Status = EdgeDeviceCredentialStatus.Active;
        ConfirmedAtUtc = confirmed;
        OverlapExpiresAtUtc = null;
        ExpiresAtUtc = null;
        return Result.Success();
    }
}
