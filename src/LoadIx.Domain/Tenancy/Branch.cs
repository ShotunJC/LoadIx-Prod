using LoadIx.Domain.Common;

namespace LoadIx.Domain.Tenancy;

public sealed class Branch : AggregateRoot<Guid>, ITenantOwned
{
    private Branch(
        Guid id,
        TenantId tenantId,
        string name,
        string code,
        Address address,
        string? contactEmail,
        string? contactPhoneNumber)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Code = code;
        Address = address;
        ContactEmail = contactEmail;
        ContactPhoneNumber = contactPhoneNumber;
        IsActive = true;
    }

    private Branch()
    {
        Name = string.Empty;
        Code = string.Empty;
        Address = null!;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public Address Address { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Branch> Create(
        Guid id,
        TenantId tenantId,
        string name,
        string code,
        Address address,
        string? contactEmail,
        string? contactPhoneNumber)
    {
        var error = DomainValidation.Required(name, "branch.name", 160)
            ?? DomainValidation.Required(code, "branch.code", 30);

        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "branch.identifiers_required",
                "Branch and tenant identifiers are required.");
        }

        return error is not null
            ? Result<Branch>.Failure(error)
            : Result<Branch>.Success(new Branch(
                id,
                tenantId,
                name.Trim(),
                code.Trim().ToUpperInvariant(),
                address,
                DomainValidation.Optional(contactEmail),
                DomainValidation.Optional(contactPhoneNumber)));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Result Update(
        string name,
        string code,
        Address address,
        string? contactEmail,
        string? contactPhoneNumber)
    {
        var error = DomainValidation.Required(name, "branch.name", 160)
            ?? DomainValidation.Required(code, "branch.code", 30);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Address = address;
        ContactEmail = DomainValidation.Optional(contactEmail);
        ContactPhoneNumber = DomainValidation.Optional(contactPhoneNumber);
        return Result.Success();
    }
}
