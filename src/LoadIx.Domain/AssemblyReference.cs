using System.Reflection;

namespace LoadIx.Domain;

/// <summary>
/// Provides a stable reference to the domain assembly for architecture tests and registration.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
