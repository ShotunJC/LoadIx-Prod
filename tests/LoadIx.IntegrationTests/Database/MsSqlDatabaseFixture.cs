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

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class MsSqlDatabaseGroup
    : ICollectionFixture<MsSqlDatabaseFixture>
{
    public const string Name = "SQL Server persistence";
}
