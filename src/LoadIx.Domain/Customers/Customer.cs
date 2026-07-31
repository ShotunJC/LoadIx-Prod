using LoadIx.Domain.Common;

namespace LoadIx.Domain.Customers;

public sealed class Customer : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<ContactPerson> _contacts = [];

    private Customer(
        Guid id,
        TenantId tenantId,
        string legalName,
        string tradingName,
        string registrationNumber,
        string? taxNumber,
        int paymentTermsDays,
        Address billingAddress,
        string? notes)
        : base(id)
    {
        TenantId = tenantId;
        LegalName = legalName;
        TradingName = tradingName;
        RegistrationNumber = registrationNumber;
        TaxNumber = taxNumber;
        PaymentTermsDays = paymentTermsDays;
        BillingAddress = billingAddress;
        Notes = notes;
        IsActive = true;
    }

    private Customer()
    {
        LegalName = string.Empty;
        TradingName = string.Empty;
        RegistrationNumber = string.Empty;
        BillingAddress = null!;
    }

    public TenantId TenantId { get; private set; }

    public string LegalName { get; private set; }

    public string TradingName { get; private set; }

    public string RegistrationNumber { get; private set; }

    public string? TaxNumber { get; private set; }

    public int PaymentTermsDays { get; private set; }

    public Address BillingAddress { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<ContactPerson> Contacts => _contacts.AsReadOnly();

    public static Result<Customer> Create(
        Guid id,
        TenantId tenantId,
        string legalName,
        string tradingName,
        string registrationNumber,
        string? taxNumber,
        int paymentTermsDays,
        Address billingAddress,
        string? notes = null)
    {
        var error = DomainValidation.Required(
                legalName,
                "customer.legal_name",
                200)
            ?? DomainValidation.Required(
                tradingName,
                "customer.trading_name",
                200)
            ?? DomainValidation.Required(
                registrationNumber,
                "customer.registration_number",
                80);

        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "customer.identifiers_required",
                "Customer and tenant identifiers are required.");
        }
        else if (paymentTermsDays is < 0 or > 365)
        {
            error = new DomainError(
                "customer.invalid_payment_terms",
                "Payment terms must be between 0 and 365 days.");
        }

        return error is not null
            ? Result<Customer>.Failure(error)
            : Result<Customer>.Success(new Customer(
                id,
                tenantId,
                legalName.Trim(),
                tradingName.Trim(),
                registrationNumber.Trim(),
                DomainValidation.Optional(taxNumber),
                paymentTermsDays,
                billingAddress,
                DomainValidation.Optional(notes)));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Result Update(
        string legalName,
        string tradingName,
        string registrationNumber,
        string? taxNumber,
        int paymentTermsDays,
        Address billingAddress,
        string? notes)
    {
        var candidate = Create(
            Id,
            TenantId,
            legalName,
            tradingName,
            registrationNumber,
            taxNumber,
            paymentTermsDays,
            billingAddress,
            notes);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        LegalName = candidate.Value.LegalName;
        TradingName = candidate.Value.TradingName;
        RegistrationNumber = candidate.Value.RegistrationNumber;
        TaxNumber = candidate.Value.TaxNumber;
        PaymentTermsDays = candidate.Value.PaymentTermsDays;
        BillingAddress = candidate.Value.BillingAddress;
        Notes = candidate.Value.Notes;
        return Result.Success();
    }
}
