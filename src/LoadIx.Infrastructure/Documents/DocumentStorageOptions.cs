using System.ComponentModel.DataAnnotations;

namespace LoadIx.Infrastructure.Documents;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "Documents";

    [Required]
    public string Provider { get; init; } = "S3Compatible";

    public string Endpoint { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    public string Bucket { get; init; } = "loadix-documents";

    public bool UseSsl { get; init; }

    [Range(1, 100)]
    public int MaximumSizeMegabytes { get; init; } = 15;

    [Range(320, 4096)]
    public int PreviewMaximumDimension { get; init; } = 1600;

    [Range(1_000_000, 200_000_000)]
    public long MaximumImagePixels { get; init; } = 40_000_000;

    public string[] AllowedExtensions { get; init; } =
        [".pdf", ".jpg", ".jpeg", ".png", ".webp"];

    [Required]
    public string MalwareScanner { get; init; } = "DevelopmentSignature";

    public long MaximumSizeBytes => MaximumSizeMegabytes * 1024L * 1024L;
}
