using FluentAssertions;
using LoadIx.Infrastructure.Tenancy;

namespace LoadIx.UnitTests.System;

public sealed class TenantClockTests
{
    [Fact]
    public void UsesTenantLocalDateAcrossTheUtcDayBoundary()
    {
        var johannesburg = TimeZoneInfo.FindSystemTimeZoneById(
            "Africa/Johannesburg");
        var instant = new DateTimeOffset(
            2026,
            8,
            7,
            22,
            30,
            0,
            TimeSpan.Zero);

        TenantClock.ToLocalDate(instant, johannesburg)
            .Should().Be(new DateOnly(2026, 8, 8));
    }
}
