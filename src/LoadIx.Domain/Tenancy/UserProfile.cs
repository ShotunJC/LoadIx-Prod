using LoadIx.Domain.Common;

namespace LoadIx.Domain.Tenancy;

public sealed class UserProfile : AggregateRoot<Guid>, ITenantOwned
{
    private UserProfile(
        Guid id,
        TenantId tenantId,
        Guid? branchId,
        string firstName,
        string lastName,
        string email,
        string? mobileNumber)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        MobileNumber = mobileNumber;
        IsActive = true;
    }

    private UserProfile()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid? BranchId { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string Email { get; private set; }

    public string? MobileNumber { get; private set; }

    public bool IsActive { get; private set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public static Result<UserProfile> Create(
        Guid id,
        TenantId tenantId,
        Guid? branchId,
        string firstName,
        string lastName,
        string email,
        string? mobileNumber)
    {
        var error = DomainValidation.Required(firstName, "user.first_name", 100)
            ?? DomainValidation.Required(lastName, "user.last_name", 100)
            ?? DomainValidation.Required(email, "user.email", 320);

        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "user.identifiers_required",
                "User profile and tenant identifiers are required.");
        }

        return error is not null
            ? Result<UserProfile>.Failure(error)
            : Result<UserProfile>.Success(new UserProfile(
                id,
                tenantId,
                branchId,
                firstName.Trim(),
                lastName.Trim(),
                email.Trim().ToLowerInvariant(),
                DomainValidation.Optional(mobileNumber)));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
