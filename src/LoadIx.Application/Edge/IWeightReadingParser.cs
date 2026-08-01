using LoadIx.Domain.Common;

namespace LoadIx.Application.Edge;

public interface IWeightReadingParser
{
    string Protocol { get; }

    string Version { get; }

    WeightParserResult Parse(
        string input,
        DateTimeOffset capturedAtUtc,
        string? correlationId = null);
}

public sealed record ParsedWeightReading(
    decimal DisplayedValue,
    WeightUnit Unit,
    bool IsStable,
    DateTimeOffset CapturedAtUtc,
    string? VehicleRegistration,
    string SerialMessageMetadata,
    string ParserVersion,
    string CorrelationId);

public sealed record WeightParserResult(
    ParsedWeightReading? Value,
    string? Code,
    string? Message)
{
    public bool IsSuccess => Value is not null;

    public static WeightParserResult Success(ParsedWeightReading value) =>
        new(value, null, null);

    public static WeightParserResult Failure(string code, string message) =>
        new(null, code, message);
}
