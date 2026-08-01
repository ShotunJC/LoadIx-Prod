using System.ComponentModel.DataAnnotations;

namespace LoadIx.Api.Configuration;

public sealed class DependencyOptions
{
    public const string SectionName = "Dependencies";

    [Required]
    public string RedisHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int RedisPort { get; init; }

    [Required]
    public string RabbitMqHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int RabbitMqPort { get; init; }

    [Required]
    public string RabbitMqUsername { get; init; } = string.Empty;

    [Required]
    public string RabbitMqPassword { get; init; } = string.Empty;

    public string RabbitMqVirtualHost { get; init; } = "/";

    [Required]
    public string ReportExportQueueName { get; init; } = "loadix.report-exports";

    [Required]
    public string MinioHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int MinioPort { get; init; }

    [Required]
    public string MailpitHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int MailpitPort { get; init; }
}
