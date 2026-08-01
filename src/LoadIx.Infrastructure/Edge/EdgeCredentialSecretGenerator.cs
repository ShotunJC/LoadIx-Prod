using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace LoadIx.Infrastructure.Edge;

public sealed class EdgeCredentialSecretGenerator
{
    public static string Create() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
