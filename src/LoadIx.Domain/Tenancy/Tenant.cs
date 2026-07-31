using LoadIx.Domain.Common;

namespace LoadIx.Domain.Tenancy;

public sealed class Tenant : AggregateRoot<TenantId>
{
    private Tenant(
        TenantId id,
        string name,
        string legalName,
        string registrationNumber,
        string? taxNumber,
        string defaultCurrency,
        string timeZone)
        : base(id)
    {
        Name = name;
        LegalName = legalName;
        RegistrationNumber = registrationNumber;
        TaxNumber = taxNumber;
        DefaultCurrency = defaultCurrency;
        TimeZone = timeZone;
        IsActive = true;
    }

    private Tenant()
    {
        Name = string.Empty;
        LegalName = string.Empty;
        RegistrationNumber = string.Empty;
        DefaultCurrency = string.Empty;
        TimeZone = string.Empty;
    }

    public string Name { get; private set; }

    public string LegalName { get; private set; }

    public string RegistrationNumber { get; private set; }

    public string? TaxNumber { get; private set; }

    public string DefaultCurrency { get; private set; }

    public string TimeZone { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Tenant> Create(
        TenantId id,
        string name,
        string legalName,
        string registrationNumber,
        string? taxNumber,
        string defaultCurrency,
        string timeZone)
    {
        var error = DomainValidation.Required(name, "tenant.name", 160)
            ?? DomainValidation.Required(legalName, "tenant.legal_name", 200)
            ?? DomainValidation.Required(
                registrationNumber,
                "tenant.registration_number",
                80)
            ?? DomainValidation.Required(timeZone, "tenant.time_zone", 100);

        if (id.IsEmpty)
        {
            error = new DomainError(
                "tenant.id_required",
                "Tenant identifier is required.");
        }

        var currencyResult = Money.Create(0m, defaultCurrency);
        error ??= currencyResult.Error;

        return error is not null
            ? Result<Tenant>.Failure(error)
            : Result<Tenant>.Success(new Tenant(
                id,
                name.Trim(),
                legalName.Trim(),
                registrationNumber.Trim(),
                DomainValidation.Optional(taxNumber),
                currencyResult.Value.CurrencyCode,
                timeZone.Trim()));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
