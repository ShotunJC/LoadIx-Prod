namespace LoadIx.Domain.Common;

public sealed record Address
{
    private Address(
        string line1,
        string? line2,
        string city,
        string provinceOrRegion,
        string postalCode,
        string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        ProvinceOrRegion = provinceOrRegion;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    private Address()
    {
        Line1 = string.Empty;
        City = string.Empty;
        ProvinceOrRegion = string.Empty;
        PostalCode = string.Empty;
        CountryCode = string.Empty;
    }

    public string Line1 { get; private set; }

    public string? Line2 { get; private set; }

    public string City { get; private set; }

    public string ProvinceOrRegion { get; private set; }

    public string PostalCode { get; private set; }

    public string CountryCode { get; private set; }

    public static Result<Address> Create(
        string line1,
        string? line2,
        string city,
        string provinceOrRegion,
        string postalCode,
        string countryCode)
    {
        if (string.IsNullOrWhiteSpace(line1)
            || string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(provinceOrRegion)
            || string.IsNullOrWhiteSpace(postalCode))
        {
            return Result<Address>.Failure(new DomainError(
                "address.required_fields",
                "Address line, city, region and postal code are required."));
        }

        var normalizedCountryCode = countryCode?.Trim().ToUpperInvariant();
        if (normalizedCountryCode is null
            || normalizedCountryCode.Length != 2
            || !normalizedCountryCode.All(char.IsAsciiLetterUpper))
        {
            return Result<Address>.Failure(new DomainError(
                "address.invalid_country",
                "Country code must contain two uppercase ISO-style letters."));
        }

        return Result<Address>.Success(new Address(
            line1.Trim(),
            string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            city.Trim(),
            provinceOrRegion.Trim(),
            postalCode.Trim(),
            normalizedCountryCode));
    }
}
