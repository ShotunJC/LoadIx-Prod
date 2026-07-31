using System.Reflection;
using FluentAssertions;

namespace LoadIx.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Theory]
    [MemberData(nameof(AllowedDependencies))]
    public void ProjectReferencesRespectLayerBoundaries(
        Assembly assembly,
        string[] allowedLoadIxDependencies)
    {
        var actualDependencies = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith(
                "LoadIx.",
                StringComparison.Ordinal))
            .Cast<string>();

        var forbiddenDependencies = actualDependencies.Except(
            allowedLoadIxDependencies,
            StringComparer.Ordinal);

        forbiddenDependencies.Should().BeEmpty();
    }

    public static TheoryData<Assembly, string[]> AllowedDependencies() =>
        new()
        {
            {
                typeof(LoadIx.Domain.AssemblyReference).Assembly,
                []
            },
            {
                typeof(LoadIx.Contracts.System.SystemInfoResponse).Assembly,
                []
            },
            {
                typeof(LoadIx.Application.DependencyInjection).Assembly,
                ["LoadIx.Domain"]
            },
            {
                typeof(LoadIx.Infrastructure.DependencyInjection).Assembly,
                ["LoadIx.Application", "LoadIx.Domain"]
            },
        };
}
