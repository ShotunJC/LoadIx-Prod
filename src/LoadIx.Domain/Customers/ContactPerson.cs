using LoadIx.Domain.Common;

namespace LoadIx.Domain.Customers;

public sealed class ContactPerson : AuditableEntity<Guid>, ITenantOwned
{
    private ContactPerson(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string name,
        string? email,
        string? phoneNumber,
        ContactCategory category)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Category = category;
        IsActive = true;
    }

    private ContactPerson()
    {
        Name = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public string? Email { get; private set; }

    public string? PhoneNumber { get; private set; }

    public ContactCategory Category { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<ContactPerson> Create(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string name,
        string? email,
        string? phoneNumber,
        ContactCategory category)
    {
        var error = DomainValidation.Required(name, "contact.name", 160);
        if (id == Guid.Empty || tenantId.IsEmpty || customerId == Guid.Empty)
        {
            error = new DomainError(
                "contact.identifiers_required",
                "Contact, customer and tenant identifiers are required.");
        }
        else if (!Enum.IsDefined(category))
        {
            error = new DomainError(
                "contact.invalid_category",
                "Contact category is not supported.");
        }
        else if (string.IsNullOrWhiteSpace(email)
                 && string.IsNullOrWhiteSpace(phoneNumber))
        {
            error = new DomainError(
                "contact.details_required",
                "A contact requires an email address or phone number.");
        }

        return error is not null
            ? Result<ContactPerson>.Failure(error)
            : Result<ContactPerson>.Success(new ContactPerson(
                id,
                tenantId,
                customerId,
                name.Trim(),
                DomainValidation.Optional(email)?.ToLowerInvariant(),
                DomainValidation.Optional(phoneNumber),
                category));
    }

    public Result Update(
        string name,
        string? email,
        string? phoneNumber,
        ContactCategory category)
    {
        var candidate = Create(
            Id,
            TenantId,
            CustomerId,
            name,
            email,
            phoneNumber,
            category);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        Name = candidate.Value.Name;
        Email = candidate.Value.Email;
        PhoneNumber = candidate.Value.PhoneNumber;
        Category = candidate.Value.Category;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
