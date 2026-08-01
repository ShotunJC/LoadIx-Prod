using LoadIx.Domain.Edge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class EdgeDeviceConfiguration : IEntityTypeConfiguration<EdgeDevice>
{
    public void Configure(EntityTypeBuilder<EdgeDevice> builder)
    {
        builder.ToTable("EdgeDevices", "edge");
        builder.ConfigureAuditableEntity<EdgeDevice, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(entity => entity.Protocol)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.ParserVersion)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.RegisteredAtUtc).HasPrecision(7);
        builder.Property(entity => entity.RevokedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.LastHeartbeatAtUtc).HasPrecision(7);
        builder.Property(entity => entity.LastAgentVersion)
            .HasMaxLength(80)
            .IsUnicode(false);
        builder.Property(entity => entity.LastHeartbeatStatus)
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.HasIndex(entity => new { entity.TenantId, entity.Name })
            .IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.SiteId,
            entity.Status,
            entity.Id,
        });
    }
}

internal sealed class EdgeDeviceCredentialConfiguration
    : IEntityTypeConfiguration<EdgeDeviceCredential>
{
    public void Configure(EntityTypeBuilder<EdgeDeviceCredential> builder)
    {
        builder.ToTable("EdgeDeviceCredentials", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.SecretHash)
            .HasMaxLength(512)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ExpiresAtUtc).HasPrecision(7);
        builder.Property(entity => entity.RevokedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.LastUsedAtUtc).HasPrecision(7);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.Type,
            entity.RevokedAtUtc,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.Type,
        })
            .IsUnique()
            .HasFilter("[RevokedAtUtc] IS NULL");
    }
}

internal sealed class WeighbridgeDeviceConfiguration
    : IEntityTypeConfiguration<WeighbridgeDevice>
{
    public void Configure(EntityTypeBuilder<WeighbridgeDevice> builder)
    {
        builder.ToTable("WeighbridgeDevices", "edge");
        builder.ConfigureAuditableEntity<WeighbridgeDevice, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(entity => entity.Code)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Protocol)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.ParserVersion)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(entity => new { entity.TenantId, entity.Code })
            .IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.SiteId,
            entity.IsActive,
            entity.Id,
        });
    }
}

internal sealed class RawWeightReadingConfiguration
    : IEntityTypeConfiguration<RawWeightReading>
{
    public void Configure(EntityTypeBuilder<RawWeightReading> builder)
    {
        builder.ToTable("RawWeightReadings", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DisplayedValue).HasPrecision(18, 3);
        builder.Property(entity => entity.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.CapturedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.VehicleRegistration)
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(entity => entity.SerialMessageMetadata)
            .HasMaxLength(4000);
        builder.Property(entity => entity.ParserVersion)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.CorrelationId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.WeighbridgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
    }
}

internal sealed class StableWeightReadingConfiguration
    : IEntityTypeConfiguration<StableWeightReading>
{
    public void Configure(EntityTypeBuilder<StableWeightReading> builder)
    {
        builder.ToTable("StableWeightReadings", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DisplayedValue).HasPrecision(18, 3);
        builder.Property(entity => entity.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.CapturedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.VehicleRegistration)
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(entity => entity.CorrelationId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new { entity.TenantId, entity.RawWeightReadingId })
            .IsUnique();
    }
}

internal sealed class DeviceHeartbeatConfiguration
    : IEntityTypeConfiguration<DeviceHeartbeat>
{
    public void Configure(EntityTypeBuilder<DeviceHeartbeat> builder)
    {
        builder.ToTable("DeviceHeartbeats", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DeviceTimestampUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.AgentVersion)
            .HasMaxLength(80)
            .IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.Property(entity => entity.MetadataJson).HasMaxLength(4000);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
    }
}

internal sealed class EdgeCommandConfiguration : IEntityTypeConfiguration<EdgeCommand>
{
    public void Configure(EntityTypeBuilder<EdgeCommand> builder)
    {
        builder.ToTable("EdgeCommands", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.CommandType)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.IdempotencyKey)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Nonce)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.RequestHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.DeviceTimestampUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.CorrelationId)
            .HasMaxLength(128)
            .IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.FailureCode)
            .HasMaxLength(120)
            .IsUnicode(false);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.Nonce,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.IdempotencyKey,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
    }
}

internal sealed class EdgeTicketSubmissionConfiguration
    : IEntityTypeConfiguration<EdgeTicketSubmission>
{
    public void Configure(EntityTypeBuilder<EdgeTicketSubmission> builder)
    {
        builder.ToTable("EdgeTicketSubmissions", "edge");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TicketNumber)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(entity => entity.CapturedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.Content).HasColumnType("nvarchar(max)");
        builder.Property(entity => entity.ContentType)
            .HasMaxLength(80)
            .IsUnicode(false);
        builder.Property(entity => entity.MetadataJson).HasMaxLength(4000);
        builder.Property(entity => entity.CorrelationId)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.EdgeDeviceId,
            entity.ReceivedAtUtc,
            entity.Id,
        });
    }
}
