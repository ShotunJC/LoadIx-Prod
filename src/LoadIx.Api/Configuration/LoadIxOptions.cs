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
}
