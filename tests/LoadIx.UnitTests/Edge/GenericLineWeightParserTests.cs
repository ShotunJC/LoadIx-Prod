using System.Globalization;
using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.EdgeSimulator.Parsing;

namespace LoadIx.UnitTests.Edge;

public sealed class GenericLineWeightParserTests
{
    private readonly GenericLineWeightParser _parser = new();

    [Fact]
    public void ParsesStableKilogramLineAndPreservesRawMetadata()
    {
        var capturedAt = DateTimeOffset.Parse(
            "2026-08-01T10:00:00Z",
            CultureInfo.InvariantCulture);

        var result = _parser.Parse(
            "WEIGHT|value=12345.250|unit=kg|stable=true|vehicle=ca-123",
            capturedAt,
            "correlation-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            DisplayedValue = 12345.250m,
            Unit = WeightUnit.Kilograms,
            IsStable = true,
            CapturedAtUtc = capturedAt,
            VehicleRegistration = "CA-123",
            SerialMessageMetadata =
                "WEIGHT|value=12345.250|unit=kg|stable=true|vehicle=ca-123",
            ParserVersion = "generic-line-v1",
            CorrelationId = "correlation-1",
        });
    }

    [Theory]
    [InlineData("WEIGH|value=1|unit=kg|stable=true")]
    [InlineData("WEIGHT|value=1.2345|unit=kg|stable=true")]
    [InlineData("WEIGHT|value=1|unit=lb|stable=true")]
    [InlineData("WEIGHT|value=1|unit=kg|stable=maybe")]
    [InlineData("WEIGHT|value=1|unit=kg|stable=true|value=2")]
    public void RejectsUnsupportedOrMalformedLines(string line)
    {
        var result = _parser.Parse(line, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().NotBeNullOrWhiteSpace();
    }
}
