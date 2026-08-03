using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace LoadIx.IntegrationTests.Database;

public sealed class MsSqlDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04")
        .WithPassword("LoadIx_Test_Only_123!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public static async Task DeleteDatabaseAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        builder.Pooling = false;
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        var identifier = $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
        command.CommandText = $"""
            IF DB_ID(@databaseName) IS NOT NULL
            BEGIN
                ALTER DATABASE {identifier} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE {identifier};
            END
            """;
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class MsSqlDatabaseGroup
    : ICollectionFixture<MsSqlDatabaseFixture>
{
    public const string Name = "SQL Server persistence";
}
