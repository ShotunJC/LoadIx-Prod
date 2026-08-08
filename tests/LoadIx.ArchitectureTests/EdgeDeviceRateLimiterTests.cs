using FluentAssertions;
using LoadIx.Api.Security;
using LoadIx.Infrastructure.Edge;
using Microsoft.Extensions.Options;

namespace LoadIx.ArchitectureTests;

public sealed class EdgeDeviceRateLimiterTests
{
    [Fact]
    public void MiddlewareAuthenticatesEdgeRequestsBeforeTrustedTenantResolution()
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "LoadIx.Api",
            "Program.cs"));

        program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal)
            .Should().BeLessThan(program.IndexOf(
                "app.UseMiddleware<EdgeDeviceRateLimitingMiddleware>();",
                StringComparison.Ordinal));
        program.IndexOf(
                "app.UseMiddleware<EdgeDeviceRateLimitingMiddleware>();",
                StringComparison.Ordinal)
            .Should().BeLessThan(program.IndexOf(
                "app.UseMiddleware<TenantContextMiddleware>();",
                StringComparison.Ordinal));
    }

    [Fact]
    public void UnauthenticatedPartitionDoesNotTrustTheDeviceHeader()
    {
        var repositoryRoot = FindRepositoryRoot();
        var middleware = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "LoadIx.Api",
            "Security",
            "EdgeDeviceRateLimitingMiddleware.cs"));
        var rawPartitionStart = middleware.IndexOf(
            "var rawPartition",
            StringComparison.Ordinal);
        var authenticationStart = middleware.IndexOf(
            "var requiresEdgeAuthentication",
            StringComparison.Ordinal);

        middleware[rawPartitionStart..authenticationStart]
            .Should().NotContain(EdgeDeviceAuthenticationDefaults.DeviceIdHeader);
        middleware[rawPartitionStart..authenticationStart]
            .Should().Contain("unauthenticated:ip:");
    }

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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "LoadIx.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
