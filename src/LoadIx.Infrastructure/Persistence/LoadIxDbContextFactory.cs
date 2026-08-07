using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LoadIx.Infrastructure.Persistence;

public sealed class LoadIxDbContextFactory
    : IDesignTimeDbContextFactory<LoadIxDbContext>
{
    public LoadIxDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString(args);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Database is required for EF Core design-time operations.");
        }

        var options = new DbContextOptionsBuilder<LoadIxDbContext>()
            .UseSqlServer(
                connectionString,
                sqlServer => sqlServer.MigrationsAssembly(
                    typeof(LoadIxDbContext).Assembly.FullName))
            .Options;

        return new LoadIxDbContext(
            options,
            new CurrentRequestContext(),
            TimeProvider.System);
    }

    private static string? GetConnectionString(string[] args)
    {
        var explicitConnection = args
            .FirstOrDefault(argument =>
                argument.StartsWith(
                    "--connection=",
                    StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection;
        }

        var environmentConnection = Environment.GetEnvironmentVariable(
            "ConnectionStrings__Database");
        if (!string.IsNullOrWhiteSpace(environmentConnection))
        {
            return environmentConnection;
        }

        var environmentName = Environment.GetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Development;
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(repositoryRoot)
            .AddJsonFile(
                Path.Combine(
                    "src",
                    "LoadIx.Api",
                    $"appsettings.{environmentName}.json"),
                optional: true,
                reloadOnChange: false)
            .Build();

        return configuration.GetConnectionString("Database");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LoadIx.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
