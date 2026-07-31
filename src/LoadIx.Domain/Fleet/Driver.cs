using LoadIx.Domain.Common;

namespace LoadIx.Domain.Fleet;

public sealed class Driver : AggregateRoot<Guid>, ITenantOwned
{
    private Driver(
        Guid id,
        TenantId tenantId,
        Guid branchId,
        string firstName,
        string lastName,
        DriverType type,
        string identificationReference,
        string mobileNumber,
        string licenceNumber,
        DateOnly licenceExpiry,
        string emergencyContactName,
        string emergencyContactNumber)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        FirstName = firstName;
        LastName = lastName;
        Type = type;
        IdentificationReference = identificationReference;
        MobileNumber = mobileNumber;
        LicenceNumber = licenceNumber;
        LicenceExpiry = licenceExpiry;
        EmergencyContactName = emergencyContactName;
        EmergencyContactNumber = emergencyContactNumber;
        IsActive = true;
    }

    private Driver()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        IdentificationReference = string.Empty;
        MobileNumber = string.Empty;
        LicenceNumber = string.Empty;
        EmergencyContactName = string.Empty;
        EmergencyContactNumber = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public DriverType Type { get; private set; }

    public string IdentificationReference { get; private set; }

    public string MobileNumber { get; private set; }

    public string LicenceNumber { get; private set; }

    public DateOnly LicenceExpiry { get; private set; }

    public string EmergencyContactName { get; private set; }

    public string EmergencyContactNumber { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? UserId { get; private set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public static Result<Driver> Create(
        Guid id,
        TenantId tenantId,
        Guid branchId,
        string firstName,
        string lastName,
        DriverType type,
        string identificationReference,
        string mobileNumber,
        string licenceNumber,
        DateOnly licenceExpiry,
        string emergencyContactName,
        string emergencyContactNumber)
    {
        var error = DomainValidation.Required(firstName, "driver.first_name", 100)
            ?? DomainValidation.Required(lastName, "driver.last_name", 100)
            ?? DomainValidation.Required(
                identificationReference,
                "driver.identification_reference",
                80)
            ?? DomainValidation.Required(mobileNumber, "driver.mobile_number", 40)
            ?? DomainValidation.Required(
                licenceNumber,
                "driver.licence_number",
                80)
            ?? DomainValidation.Required(
                emergencyContactName,
                "driver.emergency_contact_name",
                160)
            ?? DomainValidation.Required(
                emergencyContactNumber,
                "driver.emergency_contact_number",
                40);

        if (id == Guid.Empty || tenantId.IsEmpty || branchId == Guid.Empty)
        {
            error = new DomainError(
                "driver.identifiers_required",
                "Driver, branch and tenant identifiers are required.");
        }
        else if (!Enum.IsDefined(type))
        {
            error = new DomainError(
                "driver.invalid_type",
                "Driver type is not supported.");
        }

        return error is not null
            ? Result<Driver>.Failure(error)
            : Result<Driver>.Success(new Driver(
                id,
                tenantId,
                branchId,
                firstName.Trim(),
                lastName.Trim(),
                type,
                identificationReference.Trim(),
                mobileNumber.Trim(),
                licenceNumber.Trim().ToUpperInvariant(),
                licenceExpiry,
                emergencyContactName.Trim(),
                emergencyContactNumber.Trim()));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Result LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure(new DomainError(
                "driver.user_required",
                "A user identifier is required."));
        }

        UserId = userId;
        return Result.Success();
    }

    public void UnlinkUser() => UserId = null;

    public Result Update(
        Guid branchId,
        string firstName,
        string lastName,
        DriverType type,
        string identificationReference,
        string mobileNumber,
        string licenceNumber,
        DateOnly licenceExpiry,
        string emergencyContactName,
        string emergencyContactNumber)
    {
        var candidate = Create(
            Id,
            TenantId,
            branchId,
            firstName,
            lastName,
            type,
            identificationReference,
            mobileNumber,
            licenceNumber,
            licenceExpiry,
            emergencyContactName,
            emergencyContactNumber);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        BranchId = candidate.Value.BranchId;
        FirstName = candidate.Value.FirstName;
        LastName = candidate.Value.LastName;
        Type = candidate.Value.Type;
        IdentificationReference = candidate.Value.IdentificationReference;
        MobileNumber = candidate.Value.MobileNumber;
        LicenceNumber = candidate.Value.LicenceNumber;
        LicenceExpiry = candidate.Value.LicenceExpiry;
        EmergencyContactName = candidate.Value.EmergencyContactName;
        EmergencyContactNumber = candidate.Value.EmergencyContactNumber;
        return Result.Success();
    }
}
