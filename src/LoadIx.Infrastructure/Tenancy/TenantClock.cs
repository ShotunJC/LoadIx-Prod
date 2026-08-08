using LoadIx.Application.Abstractions;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Tenancy;

public sealed class TenantClock(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider)
{
    public Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        LocalDateAsync(timeProvider.GetUtcNow(), cancellationToken);

    public async Task<DateOnly> LocalDateAsync(
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var timeZone = await GetTimeZoneAsync(cancellationToken);
        return ToLocalDate(instant, timeZone);
    }

    public async Task<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> LocalDayAsync(
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var timeZone = await GetTimeZoneAsync(cancellationToken);
        var date = ToLocalDate(instant, timeZone);
        return (UtcBoundary(date, timeZone), UtcBoundary(date.AddDays(1), timeZone));
    }

    public static DateOnly ToLocalDate(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    private async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken)
    {
        var tenantId = requestContext.TenantId
            ?? throw new InvalidOperationException("A trusted tenant context is required.");
        var timeZoneId = await dbContext.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.TimeZone)
            .SingleAsync(cancellationToken);
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private static DateTimeOffset UtcBoundary(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }
}
