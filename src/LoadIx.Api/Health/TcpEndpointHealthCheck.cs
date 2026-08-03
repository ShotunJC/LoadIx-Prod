using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoadIx.Api.Health;

public sealed class TcpEndpointHealthCheck(
    string dependencyName,
    string host,
    int port) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);

            return HealthCheckResult.Healthy($"{dependencyName} is reachable.");
        }
        catch (Exception exception) when (
            exception is SocketException or IOException or ArgumentException)
        {
            return HealthCheckResult.Unhealthy(
                $"{dependencyName} is not reachable.");
        }
    }
}
