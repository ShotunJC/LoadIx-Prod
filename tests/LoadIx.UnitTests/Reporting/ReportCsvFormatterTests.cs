using FluentAssertions;
using LoadIx.Application.Reporting;

namespace LoadIx.UnitTests.Reporting;

public sealed class ReportCsvFormatterTests
{
    [Fact]
    public void HumanizesStableHeadersAndFormatsDecimalsInvariantly()
    {
        ReportCsvFormatter.HumanizeHeader("CompletedAtUtc").Should().Be("Completed At Utc");
        ReportCsvFormatter.FormatCell(1234.5m).Should().Be("1234.5");
        ReportCsvFormatter.FormatCell(new DateOnly(2026, 8, 1)).Should().Be("2026-08-01");
    }

    [Fact]
    public void EscapesCommasQuotesAndNewlines()
    {
        ReportCsvFormatter.Escape("North, \"Main\"\r\nDepot")
            .Should().Be("\"North, \"\"Main\"\"\r\nDepot\"");
    }

    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+123")]
    [InlineData("-123")]
    [InlineData("@username")]
    [InlineData("*formula")]
    public void ProtectsFormulaLikeCells(string value)
    {
        ReportCsvFormatter.Escape(value).Should().StartWith("'");
    }
}
