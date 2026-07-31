using System.Globalization;

namespace LoadIx.Domain.Common;

public sealed record Money
{
    private Money(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    private Money()
    {
        CurrencyCode = string.Empty;
    }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; }

    public static Result<Money> Create(decimal amount, string currencyCode)
    {
        var normalizedCurrency = currencyCode?.Trim().ToUpperInvariant();
        if (normalizedCurrency is null
            || normalizedCurrency.Length != 3
            || !normalizedCurrency.All(char.IsAsciiLetterUpper))
        {
            return Result<Money>.Failure(new DomainError(
                "money.invalid_currency",
                "Currency code must contain three uppercase ISO-style letters."));
        }

        if (decimal.Round(amount, 4, MidpointRounding.ToEven) != amount)
        {
            return Result<Money>.Failure(new DomainError(
                "money.invalid_precision",
                "Money supports at most four decimal places."));
        }

        return Result<Money>.Success(new Money(amount, normalizedCurrency));
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{CurrencyCode} {Amount:0.####}");
}
