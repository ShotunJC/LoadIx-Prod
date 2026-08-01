namespace LoadIx.Application.Reporting;

public readonly record struct ReportingUtcRange(
    DateTimeOffset StartUtc,
    DateTimeOffset ExclusiveEndUtc);

public static class ReportingDateRange
{
    public static ReportingUtcRange Resolve(
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone)
    {
        if (to < from) throw new ArgumentException("The end date must be on or after the start date.", nameof(to));
        return new ReportingUtcRange(LocalBoundary(from, timeZone), LocalBoundary(to.AddDays(1), timeZone));
    }

    private static DateTimeOffset LocalBoundary(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
