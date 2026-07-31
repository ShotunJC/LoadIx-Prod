using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoadIx.Api.Health;

public sealed class StartupHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("The API process is running."));
}
