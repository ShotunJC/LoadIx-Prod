using FluentAssertions;
using LoadIx.Api.Security;
using LoadIx.Infrastructure.Edge;
using Microsoft.Extensions.Options;

namespace LoadIx.ArchitectureTests;

public sealed class EdgeDeviceRateLimiterTests
{
    [Fact]
    public async Task AuthenticatedQuotaIsIndependentPerTenantAndDevice()
    {
        using var limiter = new EdgeDeviceRateLimiter(Options.Create(
            new EdgeDeviceOptions
            {
                AuthenticatedRequestPermitLimit = 1,
                AuthenticatedRequestWindowSeconds = 60,
            }));

        using var firstDevicePermit = await limiter.AcquireAsync(
            "tenant-a:device-a",
            CancellationToken.None);
        using var exhaustedDevicePermit = await limiter.AcquireAsync(
            "tenant-a:device-a",
            CancellationToken.None);
        using var secondDevicePermit = await limiter.AcquireAsync(
            "tenant-a:device-b",
            CancellationToken.None);
        using var otherTenantPermit = await limiter.AcquireAsync(
            "tenant-b:device-a",
            CancellationToken.None);

        firstDevicePermit.IsAcquired.Should().BeTrue();
        exhaustedDevicePermit.IsAcquired.Should().BeFalse();
        secondDevicePermit.IsAcquired.Should().BeTrue();
        otherTenantPermit.IsAcquired.Should().BeTrue();
    }
}
