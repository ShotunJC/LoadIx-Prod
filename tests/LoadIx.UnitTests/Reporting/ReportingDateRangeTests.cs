using FluentAssertions;
using LoadIx.Application.Reporting;

namespace LoadIx.UnitTests.Reporting;

public sealed class ReportingDateRangeTests
{
    [Fact]
    public void JohannesburgUsesTenantLocalMidnightAndExclusiveNextDay()
    {
        var range = ReportingDateRange.Resolve(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg"));

        range.StartUtc.Should().Be(new DateTimeOffset(2026, 1, 14, 22, 0, 0, TimeSpan.Zero));
        range.ExclusiveEndUtc.Should().Be(new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CompletionAtStartIsIncludedAndCompletionAtExclusiveEndIsExcluded()
    {
        var range = ReportingDateRange.Resolve(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg"));

        (range.StartUtc < range.ExclusiveEndUtc).Should().BeTrue();
        new DateTimeOffset(2026, 1, 14, 22, 0, 0, TimeSpan.Zero).Should().Be(range.StartUtc);
        new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero).Should().Be(range.ExclusiveEndUtc);
    }

    [Fact]
    public void DstTransitionDoesNotAssumeEveryTenantDayIs24Hours()
    {
        var timeZone = OperatingSystem.IsWindows()
            ? TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")
            : TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        var range = ReportingDateRange.Resolve(
            new DateOnly(2026, 3, 8),
            new DateOnly(2026, 3, 8),
            timeZone);

        (range.ExclusiveEndUtc - range.StartUtc).Should().Be(TimeSpan.FromHours(23));
    }

    [Fact]
    public void RejectsAnEndDateBeforeTheStartDate()
    {
        var act = () => ReportingDateRange.Resolve(
            new DateOnly(2026, 1, 16),
            new DateOnly(2026, 1, 15),
            TimeZoneInfo.Utc);

        act.Should().Throw<ArgumentException>();
    }
}
