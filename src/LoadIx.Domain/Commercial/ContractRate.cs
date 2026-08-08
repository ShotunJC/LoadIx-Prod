using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class ContractRate : Entity<Guid>, ITenantOwned
{
    private ContractRate(
        Guid id,
        TenantId tenantId,
        Guid contractRouteId,
        ContractRateDefinition definition)
        : base(id)
    {
        TenantId = tenantId;
        ContractRouteId = contractRouteId;
        RateType = definition.RateType;
        EffectiveFrom = definition.EffectiveFrom;
        EffectiveTo = definition.EffectiveTo;
        FixedAmount = definition.FixedAmount;
        BaseFee = definition.BaseFee;
        PerTonneAmount = definition.PerTonneAmount;
        MinimumCharge = definition.MinimumCharge;
        FuelSurchargePercent = definition.FuelSurchargePercent;
        RoundingDecimalPlaces = definition.RoundingDecimalPlaces;
        RoundingMode = definition.RoundingMode;
    }

    private ContractRate()
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid TransportContractId { get; private set; }

    public Guid ContractRouteId { get; private set; }

    public ContractRateType RateType { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public decimal? FixedAmount { get; private set; }

    public decimal? BaseFee { get; private set; }

    public decimal? PerTonneAmount { get; private set; }

    public decimal? MinimumCharge { get; private set; }

    public decimal? FuelSurchargePercent { get; private set; }

    public int RoundingDecimalPlaces { get; private set; }

    public MidpointRounding RoundingMode { get; private set; }

    internal static Result<ContractRate> Create(
        Guid id,
        TenantId tenantId,
        ContractRateDefinition definition)
    {
        var error = Validate(definition);
        if (id == Guid.Empty || tenantId.IsEmpty || definition.ContractRouteId == Guid.Empty)
        {
            error = new DomainError(
                "contract.rate_identifiers_required",
                "Rate, tenant, and route identifiers are required.");
        }

        return error is null
            ? Result<ContractRate>.Success(new ContractRate(
                id, tenantId, definition.ContractRouteId, definition))
            : Result<ContractRate>.Failure(error);
    }

    internal Result Update(ContractRateDefinition definition)
    {
        var candidate = Create(Id, TenantId, definition);
        if (candidate.IsFailure) return Result.Failure(candidate.Error!);
        ContractRouteId = candidate.Value.ContractRouteId;
        RateType = candidate.Value.RateType;
        EffectiveFrom = candidate.Value.EffectiveFrom;
        EffectiveTo = candidate.Value.EffectiveTo;
        FixedAmount = candidate.Value.FixedAmount;
        BaseFee = candidate.Value.BaseFee;
        PerTonneAmount = candidate.Value.PerTonneAmount;
        MinimumCharge = candidate.Value.MinimumCharge;
        FuelSurchargePercent = candidate.Value.FuelSurchargePercent;
        RoundingDecimalPlaces = candidate.Value.RoundingDecimalPlaces;
        RoundingMode = candidate.Value.RoundingMode;
        return Result.Success();
    }

    private static DomainError? Validate(ContractRateDefinition definition)
    {
        if (!Enum.IsDefined(definition.RateType))
        {
            return new DomainError("contract.rate_type_invalid", "Rate type is not supported.");
        }

        if (definition.EffectiveTo < definition.EffectiveFrom)
        {
            return new DomainError(
                "contract.rate_dates_invalid",
                "Rate end date cannot precede its start date.");
        }

        if (definition.RoundingDecimalPlaces is < 0 or > 4
            || definition.RoundingMode is not (
                MidpointRounding.ToEven or MidpointRounding.AwayFromZero))
        {
            return new DomainError(
                "contract.rounding_invalid",
                "Rounding must use 0 to 4 decimal places and a supported midpoint mode.");
        }

        if (new[]
            {
                definition.FixedAmount,
                definition.BaseFee,
                definition.PerTonneAmount,
                definition.MinimumCharge,
                definition.FuelSurchargePercent,
            }.Any(value => value < 0))
        {
            return new DomainError(
                "contract.rate_negative",
                "Rate amounts and percentages cannot be negative.");
        }

        var requiresFixed = definition.RateType == ContractRateType.FixedPerTrip;
        var requiresBase = definition.RateType == ContractRateType.BasePlusPerTonne;
        var requiresPerTonne = definition.RateType is not ContractRateType.FixedPerTrip;
        if (requiresFixed && definition.FixedAmount is null
            || requiresBase && definition.BaseFee is null
            || requiresPerTonne && definition.PerTonneAmount is null)
        {
            return new DomainError(
                "contract.rate_amount_required",
                "The selected rate type is missing a required amount.");
        }

        if (definition.FuelSurchargePercent > 100)
        {
            return new DomainError(
                "contract.fuel_surcharge_invalid",
                "Fuel surcharge percentage cannot exceed 100.");
        }

        return null;
    }
}

public sealed record ContractRateDefinition(
    Guid? Id,
    Guid ContractRouteId,
    ContractRateType RateType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? FixedAmount,
    decimal? BaseFee,
    decimal? PerTonneAmount,
    decimal? MinimumCharge,
    decimal? FuelSurchargePercent,
    int RoundingDecimalPlaces = 2,
    MidpointRounding RoundingMode = MidpointRounding.AwayFromZero);
