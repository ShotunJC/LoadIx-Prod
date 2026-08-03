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
    /// Optional single trusted reverse-proxy address. Forwarded headers are
    /// ignored unless this is explicitly configured.
    /// </summary>
    public string? TrustedProxyIp { get; init; }
}
