using System.ComponentModel.DataAnnotations;

namespace LoadIx.Api.Configuration;

public sealed class LoadIxOptions
{
    public const string SectionName = "LoadIx";

    [Required]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    public string Version { get; init; } = string.Empty;

    [Required]
    public string WebOrigin { get; init; } = string.Empty;

    /// <summary>
    /// API hosts keep this enabled for local development. In the pilot, set it
    /// only on the dedicated worker host so horizontally scaled API replicas do
    /// not compete for background work.
    /// </summary>
    public bool RunBackgroundWorkers { get; init; } = true;

    /// <summary>
    /// Allows plaintext connections only to dependencies isolated on the same
    /// trusted host/private container network. Keep false for remote endpoints.
    /// </summary>
    public bool AllowInsecurePrivateDependencies { get; init; }

    /// <summary>
    /// Optional single trusted reverse-proxy address. Forwarded headers are
    /// ignored unless this is explicitly configured.
    /// </summary>
    public string? TrustedProxyIp { get; init; }
}
