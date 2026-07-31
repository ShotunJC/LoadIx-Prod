using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class Commodity : AggregateRoot<Guid>, ITenantOwned
{
    private Commodity(
        Guid id,
        TenantId tenantId,
        string name,
        string code,
        WeightUnit defaultWeightUnit)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Code = code;
        DefaultWeightUnit = defaultWeightUnit;
        IsActive = true;
    }

    private Commodity()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public WeightUnit DefaultWeightUnit { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Commodity> Create(
        Guid id,
        TenantId tenantId,
        string name,
        string code,
        WeightUnit defaultWeightUnit)
    {
        var error = DomainValidation.Required(name, "commodity.name", 160)
            ?? DomainValidation.Required(code, "commodity.code", 40);

        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "commodity.identifiers_required",
                "Commodity and tenant identifiers are required.");
        }
        else if (!Enum.IsDefined(defaultWeightUnit))
        {
            error = new DomainError(
                "commodity.invalid_unit",
                "Commodity weight unit is not supported.");
        }

        return error is not null
            ? Result<Commodity>.Failure(error)
            : Result<Commodity>.Success(new Commodity(
                id,
                tenantId,
                name.Trim(),
                code.Trim().ToUpperInvariant(),
                defaultWeightUnit));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Result Update(string name, string code, WeightUnit defaultWeightUnit)
    {
        var candidate = Create(Id, TenantId, name, code, defaultWeightUnit);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        Name = candidate.Value.Name;
        Code = candidate.Value.Code;
        DefaultWeightUnit = candidate.Value.DefaultWeightUnit;
        return Result.Success();
    }
}
