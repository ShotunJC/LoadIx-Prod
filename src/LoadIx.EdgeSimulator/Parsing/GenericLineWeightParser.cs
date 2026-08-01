using System.Globalization;
using LoadIx.Application.Edge;
using LoadIx.Domain.Common;

namespace LoadIx.EdgeSimulator.Parsing;

public sealed class GenericLineWeightParser : IWeightReadingParser
{
    public string Protocol => "generic-line";

    public string Version => "generic-line-v1";

    public WeightParserResult Parse(
        string input,
        DateTimeOffset capturedAtUtc,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return WeightParserResult.Failure(
                "parser.input_required",
                "A line is required.");
        }

        var parts = input.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length == 0
            || !string.Equals(parts[0], "WEIGHT", StringComparison.OrdinalIgnoreCase))
        {
            return WeightParserResult.Failure(
                "parser.message_type_invalid",
                "The generic line must start with WEIGHT.");
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                return WeightParserResult.Failure(
                    "parser.field_invalid",
                    "Each field must use key=value syntax.");
            }

            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (!fields.TryAdd(key, value))
            {
                return WeightParserResult.Failure(
                    "parser.field_duplicate",
                    $"The field '{key}' was supplied more than once.");
            }
        }

        if (!fields.TryGetValue("value", out var valueText)
            || !decimal.TryParse(
                valueText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var displayedValue))
        {
            return WeightParserResult.Failure(
                "parser.value_invalid",
                "A decimal value field is required.");
        }

        if (displayedValue < 0m
            || decimal.Round(displayedValue, 3, MidpointRounding.ToEven)
                != displayedValue)
        {
            return WeightParserResult.Failure(
                "parser.value_invalid",
                "The displayed value must be non-negative with at most three decimals.");
        }

        if (!fields.TryGetValue("unit", out var unitText)
            || !TryParseUnit(unitText, out var unit))
        {
            return WeightParserResult.Failure(
                "parser.unit_invalid",
                "The unit must be kg/kilograms or t/tonnes.");
        }

        if (!fields.TryGetValue("stable", out var stableText)
            || !bool.TryParse(stableText, out var isStable))
        {
            return WeightParserResult.Failure(
                "parser.stable_invalid",
                "A boolean stable field is required.");
        }

        fields.TryGetValue("vehicle", out var vehicleRegistration);
        if (vehicleRegistration?.Length > 30)
        {
            return WeightParserResult.Failure(
                "parser.vehicle_too_long",
                "Vehicle registration cannot exceed 30 characters.");
        }

        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim();
        if (resolvedCorrelationId.Length > 128)
        {
            return WeightParserResult.Failure(
                "parser.correlation_too_long",
                "Correlation ID cannot exceed 128 characters.");
        }

        return WeightParserResult.Success(new ParsedWeightReading(
            displayedValue,
            unit,
            isStable,
            capturedAtUtc.ToUniversalTime(),
            string.IsNullOrWhiteSpace(vehicleRegistration)
                ? null
                : vehicleRegistration.ToUpperInvariant(),
            input.Trim(),
            Version,
            resolvedCorrelationId));
    }

    private static bool TryParseUnit(string value, out WeightUnit unit)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "kg":
            case "kgs":
            case "kilogram":
            case "kilograms":
                unit = WeightUnit.Kilograms;
                return true;
            case "t":
            case "tonne":
            case "tonnes":
                unit = WeightUnit.Tonnes;
                return true;
            default:
                unit = default;
                return false;
        }
    }
}
