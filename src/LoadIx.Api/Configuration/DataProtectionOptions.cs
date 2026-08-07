using System.ComponentModel.DataAnnotations;

namespace LoadIx.Api.Configuration;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    [Required]
    public string Provider { get; init; } = "FileSystem";

    [Required]
    public string ApplicationName { get; init; } = "LoadIx";

    public string KeyRingPath { get; init; } = "/home/app/.aspnet/DataProtection-Keys";

}
