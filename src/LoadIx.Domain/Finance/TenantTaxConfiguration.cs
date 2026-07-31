using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class TenantTaxConfiguration : Entity<Guid>, ITenantOwned
{
    private TenantTaxConfiguration()
    {
        DefaultTaxTreatment = TaxTreatment.NotReviewed;
    }

    private TenantTaxConfiguration(
        Guid id,
        TenantId tenantId,
        TaxTreatment defaultTaxTreatment,
        decimal taxRatePercent,
        Guid actorUserId,
        DateTimeOffset reviewedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        DefaultTaxTreatment = defaultTaxTreatment;
        TaxRatePercent = decimal.Round(taxRatePercent, 4, MidpointRounding.AwayFromZero);
        ReviewedByUserId = actorUserId;
        ReviewedAtUtc = reviewedAtUtc.ToUniversalTime();
    }

    public TenantId TenantId { get; private set; }
    public TaxTreatment DefaultTaxTreatment { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsReviewed => ReviewedAtUtc.HasValue;

    public static Result<TenantTaxConfiguration> Create(
        Guid id,
        TenantId tenantId,
        TaxTreatment defaultTaxTreatment,
        decimal taxRatePercent,
        Guid actorUserId,
        DateTimeOffset reviewedAtUtc)
    {
        var error = Validate(defaultTaxTreatment, taxRatePercent);
        if (id == Guid.Empty || tenantId.IsEmpty || actorUserId == Guid.Empty
            || reviewedAtUtc.Offset != TimeSpan.Zero)
        {
            error = new DomainError(
                "finance.tax_context_invalid",
                "Tax configuration requires tenant, actor, and UTC timestamp.");
        }

        return error is null
            ? Result<TenantTaxConfiguration>.Success(new TenantTaxConfiguration(
                id,
                tenantId,
                defaultTaxTreatment,
                taxRatePercent,
                actorUserId,
                reviewedAtUtc))
            : Result<TenantTaxConfiguration>.Failure(error);
    }

    public Result Update(
        TaxTreatment defaultTaxTreatment,
        decimal taxRatePercent,
        Guid actorUserId,
        DateTimeOffset reviewedAtUtc)
    {
        var error = Validate(defaultTaxTreatment, taxRatePercent);
        if (actorUserId == Guid.Empty || reviewedAtUtc.Offset != TimeSpan.Zero)
        {
            error = new DomainError(
                "finance.tax_context_invalid",
                "Tax configuration requires an actor and UTC timestamp.");
        }

        if (error is not null) return Result.Failure(error);
        DefaultTaxTreatment = defaultTaxTreatment;
        TaxRatePercent = decimal.Round(taxRatePercent, 4, MidpointRounding.AwayFromZero);
        ReviewedByUserId = actorUserId;
        ReviewedAtUtc = reviewedAtUtc.ToUniversalTime();
        return Result.Success();
    }

    private static DomainError? Validate(
        TaxTreatment defaultTaxTreatment,
        decimal taxRatePercent)
    {
        if (!Enum.IsDefined(defaultTaxTreatment)
            || defaultTaxTreatment == TaxTreatment.NotReviewed
            || taxRatePercent < 0
            || taxRatePercent > 100)
        {
            return new DomainError(
                "finance.tax_invalid",
                "Tenant tax treatment and rate must be explicit and reviewed.");
        }

        return null;
    }
}
