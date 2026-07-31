using LoadIx.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class OperationalOutboxConfiguration : IEntityTypeConfiguration<OperationalOutboxMessage>
{
    public void Configure(EntityTypeBuilder<OperationalOutboxMessage> builder)
    {
        builder.ToTable("OperationalOutboxMessages", "operations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.EventType).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(item => item.Payload).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(1000);
        builder.Property(item => item.OccurredAtUtc).HasPrecision(3);
        builder.Property(item => item.DispatchedAtUtc).HasPrecision(3);
        builder.HasIndex(item => new { item.DispatchedAtUtc, item.OccurredAtUtc, item.Id });
        builder.HasIndex(item => new { item.TenantId, item.TripId, item.OccurredAtUtc });
    }
}
