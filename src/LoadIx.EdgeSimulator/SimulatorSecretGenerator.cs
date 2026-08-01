using System.Security.Cryptography;

namespace LoadIx.EdgeSimulator;

internal static class SimulatorSecretGenerator
{
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string CreateRequestKey(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}";
}
