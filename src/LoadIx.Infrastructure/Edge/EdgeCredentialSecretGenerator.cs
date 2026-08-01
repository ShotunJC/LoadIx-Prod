using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace LoadIx.Infrastructure.Edge;

public sealed class EdgeCredentialSecretGenerator
{
    public static string Create() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static bool IsValid(string? secret) =>
        !string.IsNullOrWhiteSpace(secret)
        && secret.Length is >= 32 and <= 256
        && secret.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or '~');
}
