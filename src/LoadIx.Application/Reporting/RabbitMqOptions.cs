namespace LoadIx.Application.Reporting;

public sealed class RabbitMqOptions
{
    public const string SectionName = "Dependencies";

    public string RabbitMqHost { get; init; } = "localhost";
    public int RabbitMqPort { get; init; } = 5672;
    public string RabbitMqUsername { get; init; } = "loadix";
    public string RabbitMqPassword { get; init; } = string.Empty;
    public string RabbitMqVirtualHost { get; init; } = "/";
    public string ReportExportQueueName { get; init; } = "loadix.report-exports";
    public string ReportExportDeadLetterQueueName { get; init; } = "loadix.report-exports.dead-letter";
}
