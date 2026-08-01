using System.ComponentModel.DataAnnotations;

namespace LoadIx.Infrastructure.Edge;

public sealed class EdgeDeviceOptions
{
    public const string SectionName = "EdgeDevices";

    [Range(30, 900)]
    public int TimestampToleranceSeconds { get; set; } = 300;

    [Range(5, 1440)]
    public int BootstrapLifetimeMinutes { get; set; } = 30;

    [Range(1, 1440)]
    public int CredentialOverlapMinutes { get; set; } = 15;

    [Range(30, 3600)]
    public int CredentialCleanupIntervalSeconds { get; set; } = 300;

    [Range(30, 3600)]
    public int OfflineAfterSeconds { get; set; } = 180;

    [Range(10, 900)]
    public int HeartbeatIntervalSeconds { get; set; } = 60;

    [Range(1024, 64_000)]
    public int MaximumTicketCharacters { get; set; } = 64_000;

    [Range(1, 10_000)]
    public int AuthenticatedRequestPermitLimit { get; set; } = 60;

    [Range(1, 3600)]
    public int AuthenticatedRequestWindowSeconds { get; set; } = 60;
}
