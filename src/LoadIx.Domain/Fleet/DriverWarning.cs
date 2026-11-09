using LoadIx.Domain.Common;

namespace LoadIx.Domain.Fleet;

public sealed class DriverWarning : AggregateRoot<Guid>, ITenantOwned
{
    private DriverWarning(
        Guid id,
        TenantId tenantId,
        Guid driverId,
        string warningNumber,
        DriverWarningType warningType,
        DriverWarningCategory category,
        DateTimeOffset incidentDateUtc,
        DateTimeOffset issuedDateUtc,
        DateTimeOffset expiryDateUtc,
        string description,
        Guid issuedByUserId,
        Guid? tripId,
        Guid? documentId)
        : base(id)
    {
        TenantId = tenantId;
        DriverId = driverId;
        WarningNumber = warningNumber.Trim().ToUpperInvariant();
        WarningType = warningType;
        Category = category;
        IncidentDateUtc = incidentDateUtc;
        IssuedDateUtc = issuedDateUtc;
        ExpiryDateUtc = expiryDateUtc;
        Description = description.Trim();
        IssuedByUserId = issuedByUserId;
        TripId = tripId;
        DocumentId = documentId;
        Status = DriverWarningStatus.Active;
    }

    private DriverWarning()
    {
        WarningNumber = string.Empty;
        Description = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid DriverId { get; private set; }

    public string WarningNumber { get; private set; }

    public DriverWarningType WarningType { get; private set; }

    public DriverWarningCategory Category { get; private set; }

    public DriverWarningStatus Status { get; private set; }

    public DateTimeOffset IncidentDateUtc { get; private set; }

    public DateTimeOffset IssuedDateUtc { get; private set; }

    public DateTimeOffset ExpiryDateUtc { get; private set; }

    public string Description { get; private set; }

    public Guid IssuedByUserId { get; private set; }

    public Guid? TripId { get; private set; }

    public Guid? DocumentId { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevocationReason { get; private set; }

    public static Result<DriverWarning> Create(
        Guid id,
        TenantId tenantId,
        Guid driverId,
        string warningNumber,
        DriverWarningType warningType,
        DriverWarningCategory category,
        DateTimeOffset incidentDateUtc,
        DateTimeOffset issuedDateUtc,
        DateTimeOffset expiryDateUtc,
        string description,
        Guid issuedByUserId,
        Guid? tripId = null,
        Guid? documentId = null)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || driverId == Guid.Empty || issuedByUserId == Guid.Empty)
        {
            return Result<DriverWarning>.Failure(new DomainError(
                "driver_warning.identifiers_required",
                "Warning ID, Tenant ID, Driver ID, and IssuedBy User ID are required."));
        }

        if (incidentDateUtc.Offset != TimeSpan.Zero || issuedDateUtc.Offset != TimeSpan.Zero || expiryDateUtc.Offset != TimeSpan.Zero)
        {
            return Result<DriverWarning>.Failure(new DomainError(
                "driver_warning.timestamps_utc_required",
                "Timestamps must be UTC."));
        }

        if (expiryDateUtc <= issuedDateUtc)
        {
            return Result<DriverWarning>.Failure(new DomainError(
                "driver_warning.invalid_expiry",
                "Warning expiry date must be after the issue date."));
        }

        var error = DomainValidation.Required(warningNumber, "driver_warning.warning_number", 60)
            ?? DomainValidation.Required(description, "driver_warning.description", 2000);

        if (error is not null)
        {
            return Result<DriverWarning>.Failure(error);
        }

        if (!Enum.IsDefined(warningType))
        {
            return Result<DriverWarning>.Failure(new DomainError(
                "driver_warning.invalid_type",
                "Unsupported driver warning type."));
        }

        if (!Enum.IsDefined(category))
        {
            return Result<DriverWarning>.Failure(new DomainError(
                "driver_warning.invalid_category",
                "Unsupported driver warning category."));
        }

        return Result<DriverWarning>.Success(new DriverWarning(
            id,
            tenantId,
            driverId,
            warningNumber,
            warningType,
            category,
            incidentDateUtc,
            issuedDateUtc,
            expiryDateUtc,
            description,
            issuedByUserId,
            tripId,
            documentId));
    }

    public Result Revoke(string reason, Guid revokedByUserId, DateTimeOffset revokedAtUtc)
    {
        if (revokedByUserId == Guid.Empty || revokedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "driver_warning.revoke_context_invalid",
                "Revocation requires an actor and UTC timestamp."));
        }

        if (Status != DriverWarningStatus.Active)
        {
            return Result.Failure(new DomainError(
                "driver_warning.cannot_revoke",
                "Only active warnings can be revoked."));
        }

        var error = DomainValidation.Required(reason, "driver_warning.revocation_reason", 500);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Status = DriverWarningStatus.Revoked;
        RevocationReason = reason.Trim();
        RevokedByUserId = revokedByUserId;
        RevokedAtUtc = revokedAtUtc;
        return Result.Success();
    }

    public void CheckAndApplyExpiry(DateTimeOffset nowUtc)
    {
        if (Status == DriverWarningStatus.Active && nowUtc >= ExpiryDateUtc)
        {
            Status = DriverWarningStatus.Expired;
        }
    }
}
