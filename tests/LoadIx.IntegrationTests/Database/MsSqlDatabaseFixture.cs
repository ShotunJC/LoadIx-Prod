using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace LoadIx.IntegrationTests.Database;

public sealed class MsSqlDatabaseFixture : IAsyncLifetime
{
    private const string TemplateBackupPath = "/var/opt/mssql/data/loadix-test-template.bak";
    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04")
        .WithPassword("LoadIx_Test_Only_123!")
        .Build();
    private bool templateReady;

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task<TestDatabaseLease> CreateDatabaseAsync(string prefix)
    {
        var databaseName = $"{prefix}_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = databaseName,
        };
        if (!templateReady)
        {
            return new TestDatabaseLease(builder.ConnectionString, true);
        }

        await RestoreTemplateAsync(databaseName);
        return new TestDatabaseLease(builder.ConnectionString, false);
    }

    public async Task CaptureTemplateAsync(string connectionString)
    {
        if (templateReady)
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        await using var connection = await OpenMasterConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = $"BACKUP DATABASE {Quote(databaseName)} TO DISK = @path WITH COPY_ONLY, INIT";
        command.Parameters.AddWithValue("@path", TemplateBackupPath);
        await command.ExecuteNonQueryAsync();
        templateReady = true;
    }

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

    private async Task RestoreTemplateAsync(string databaseName)
    {
        await using var connection = await OpenMasterConnectionAsync();
        string dataLogicalName;
        string logLogicalName;
        await using (var files = connection.CreateCommand())
        {
            files.CommandTimeout = 120;
            files.CommandText = "RESTORE FILELISTONLY FROM DISK = @path";
            files.Parameters.AddWithValue("@path", TemplateBackupPath);
            await using var reader = await files.ExecuteReaderAsync();
            dataLogicalName = string.Empty;
            logLogicalName = string.Empty;
            while (await reader.ReadAsync())
            {
                var logicalName = reader.GetString(reader.GetOrdinal("LogicalName"));
                var type = reader.GetString(reader.GetOrdinal("Type"));
                if (type == "D") dataLogicalName = logicalName;
                if (type == "L") logLogicalName = logicalName;
            }
        }

        if (string.IsNullOrWhiteSpace(dataLogicalName)
            || string.IsNullOrWhiteSpace(logLogicalName))
        {
            throw new InvalidOperationException("The SQL test template backup is incomplete.");
        }

        await using var restore = connection.CreateCommand();
        restore.CommandTimeout = 120;
        restore.CommandText = $"""
            RESTORE DATABASE {Quote(databaseName)} FROM DISK = @path
            WITH MOVE @dataLogicalName TO @dataPath,
                 MOVE @logLogicalName TO @logPath,
                 RECOVERY
            """;
        restore.Parameters.AddWithValue("@path", TemplateBackupPath);
        restore.Parameters.AddWithValue("@dataLogicalName", dataLogicalName);
        restore.Parameters.AddWithValue("@logLogicalName", logLogicalName);
        restore.Parameters.AddWithValue("@dataPath", $"/var/opt/mssql/data/{databaseName}.mdf");
        restore.Parameters.AddWithValue("@logPath", $"/var/opt/mssql/data/{databaseName}_log.ldf");
        await restore.ExecuteNonQueryAsync();
    }

    private async Task<SqlConnection> OpenMasterConnectionAsync()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "master",
            Pooling = false,
        };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed record TestDatabaseLease(
    string ConnectionString,
    bool RequiresInitialization);

[CollectionDefinition(Name)]
public sealed class MsSqlDatabaseGroup
    : ICollectionFixture<MsSqlDatabaseFixture>
{
    public const string Name = "SQL Server persistence";
}
