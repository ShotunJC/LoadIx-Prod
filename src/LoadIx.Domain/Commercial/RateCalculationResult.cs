using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed record RateCalculationInput(
    decimal? SourceNetTonnes,
    decimal? DestinationNetTonnes,
    decimal? ReconciledTonnes,
    DateOnly CalculationDate);

public sealed record RateCalculationLine(
    string Code,
    string Description,
    decimal Quantity,
    decimal Rate,
    decimal Amount);

public sealed record ContractRateSnapshot(
    Guid RateId,
    ContractRateType RateType,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? FixedAmount,
    decimal? BaseFee,
    decimal? PerTonneAmount,
    decimal? MinimumCharge,
    decimal? FuelSurchargePercent,
    int RoundingDecimalPlaces,
    MidpointRounding RoundingMode,
    string CurrencyCode);

public sealed record RateCalculationResult(
    Money Total,
    decimal BillableTonnes,
    IReadOnlyList<RateCalculationLine> Breakdown,
    ContractRateSnapshot RateSnapshot,
    RateCalculationInput InputSnapshot);

public static class ContractRateCalculator
{
    public static Result<RateCalculationResult> Calculate(
        ContractRate rate,
        RateCalculationInput input,
        string currencyCode)
    {
        if (input.CalculationDate < rate.EffectiveFrom
            || rate.EffectiveTo is not null
            && input.CalculationDate > rate.EffectiveTo.Value)
        {
            return Result<RateCalculationResult>.Failure(new DomainError(
                "rate.not_effective",
                "The selected rate is not effective on the calculation date."));
        }

        var source = ValidateWeight(input.SourceNetTonnes);
        var destination = ValidateWeight(input.DestinationNetTonnes);
        var reconciled = ValidateWeight(input.ReconciledTonnes);
        if (source is not null || destination is not null || reconciled is not null)
        {
            return Result<RateCalculationResult>.Failure(
                source ?? destination ?? reconciled!);
        }

        var tonnes = rate.RateType switch
        {
            ContractRateType.SourceNetTonne => input.SourceNetTonnes,
            ContractRateType.DestinationNetTonne => input.DestinationNetTonnes,
            ContractRateType.ReconciledTonne => input.ReconciledTonnes,
            ContractRateType.BasePlusPerTonne => input.ReconciledTonnes,
            _ => 0m,
        };
        if (rate.RateType != ContractRateType.FixedPerTrip && tonnes is null)
        {
            return Result<RateCalculationResult>.Failure(new DomainError(
                "rate.weight_required",
                $"A {rate.RateType} calculation requires its applicable net weight."));
        }

        var billableTonnes = decimal.Round(
            tonnes ?? 0m, 3, MidpointRounding.AwayFromZero);
        var lines = new List<RateCalculationLine>();
        decimal subtotal;
        if (rate.RateType == ContractRateType.FixedPerTrip)
        {
            subtotal = rate.FixedAmount!.Value;
            lines.Add(new RateCalculationLine(
                "fixed", "Fixed trip amount", 1m, subtotal, subtotal));
        }
        else
        {
            var baseFee = rate.BaseFee ?? 0m;
            var variable = billableTonnes * rate.PerTonneAmount!.Value;
            if (baseFee > 0)
            {
                lines.Add(new RateCalculationLine(
                    "base", "Base fee", 1m, baseFee, baseFee));
            }

            lines.Add(new RateCalculationLine(
                "tonnage", "Billable tonnage", billableTonnes,
                rate.PerTonneAmount.Value, variable));
            subtotal = baseFee + variable;
        }

        if (rate.FuelSurchargePercent > 0)
        {
            var surcharge = subtotal * rate.FuelSurchargePercent.Value / 100m;
            lines.Add(new RateCalculationLine(
                "fuel", "Fuel surcharge", rate.FuelSurchargePercent.Value,
                subtotal, surcharge));
            subtotal += surcharge;
        }

        if (rate.MinimumCharge is not null && subtotal < rate.MinimumCharge.Value)
        {
            lines.Add(new RateCalculationLine(
                "minimum", "Minimum charge adjustment", 1m,
                rate.MinimumCharge.Value, rate.MinimumCharge.Value - subtotal));
            subtotal = rate.MinimumCharge.Value;
        }

        var rounded = decimal.Round(
            subtotal, rate.RoundingDecimalPlaces, rate.RoundingMode);
        var money = Money.Create(rounded, currencyCode);
        if (money.IsFailure)
        {
            return Result<RateCalculationResult>.Failure(money.Error!);
        }

        var snapshot = new ContractRateSnapshot(
            rate.Id, rate.RateType, rate.EffectiveFrom, rate.EffectiveTo,
            rate.FixedAmount, rate.BaseFee, rate.PerTonneAmount,
            rate.MinimumCharge, rate.FuelSurchargePercent,
            rate.RoundingDecimalPlaces, rate.RoundingMode,
            money.Value.CurrencyCode);
        return Result<RateCalculationResult>.Success(new RateCalculationResult(
            money.Value, billableTonnes, lines.AsReadOnly(), snapshot, input));
    }

    private static DomainError? ValidateWeight(decimal? value)
    {
        if (value < 0)
        {
            return new DomainError(
                "rate.weight_negative",
                "Calculation weights cannot be negative.");
        }

        return value is not null
            && decimal.Round(value.Value, 3, MidpointRounding.ToEven) != value
            ? new DomainError(
                "rate.weight_precision",
                "Calculation weights support at most three decimal places.")
            : null;
    }
}
