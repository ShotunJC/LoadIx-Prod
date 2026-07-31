namespace LoadIx.Domain.Common;

public sealed record Weight
{
    private const decimal KilogramsPerTonne = 1000m;

    private Weight(decimal value, WeightUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    private Weight()
    {
    }

    public decimal Value { get; private set; }

    public WeightUnit Unit { get; private set; }

    public static Result<Weight> Create(decimal value, WeightUnit unit)
    {
        if (value < 0)
        {
            return Result<Weight>.Failure(new DomainError(
                "weight.negative",
                "Weight cannot be negative."));
        }

        if (!Enum.IsDefined(unit))
        {
            return Result<Weight>.Failure(new DomainError(
                "weight.invalid_unit",
                "Weight unit is not supported."));
        }

        if (decimal.Round(value, 3, MidpointRounding.ToEven) != value)
        {
            return Result<Weight>.Failure(new DomainError(
                "weight.invalid_precision",
                "Weight supports at most three decimal places."));
        }

        return Result<Weight>.Success(new Weight(value, unit));
    }

    public Weight ConvertTo(WeightUnit targetUnit)
    {
        if (targetUnit == Unit)
        {
            return this;
        }

        var converted = targetUnit switch
        {
            WeightUnit.Kilograms => Value * KilogramsPerTonne,
            WeightUnit.Tonnes => Value / KilogramsPerTonne,
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetUnit),
                targetUnit,
                "Weight unit is not supported."),
        };

        return Create(converted, targetUnit).Value;
    }
}
