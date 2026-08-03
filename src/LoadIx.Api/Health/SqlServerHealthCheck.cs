using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoadIx.Api.Health;

public sealed class SqlServerHealthCheck(string connectionString) : IHealthCheck
{
    private readonly string _healthConnectionString =
        new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 3,
        }.ConnectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_healthConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 3;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("LoadIx SQL database is reachable.");
        }
        catch (Exception exception) when (
            exception is SqlException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }
    }
}
