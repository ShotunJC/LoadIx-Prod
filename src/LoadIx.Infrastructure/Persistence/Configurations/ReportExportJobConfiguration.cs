using LoadIx.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("ReportExportJobs", "reporting");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.Report).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(item => item.FromDate).HasColumnType("date");
        builder.Property(item => item.ToDate).HasColumnType("date");
        builder.Property(item => item.CreatedAtUtc).HasPrecision(7).IsRequired();
        builder.Property(item => item.StartedAtUtc).HasPrecision(7);
        builder.Property(item => item.CompletedAtUtc).HasPrecision(7);
        builder.Property(item => item.LastHeartbeatAtUtc).HasPrecision(7);
        builder.Property(item => item.LeaseExpiresAtUtc).HasPrecision(7);
        builder.Property(item => item.LastPublishedAtUtc).HasPrecision(7);
        builder.Property(item => item.Error).HasMaxLength(1000);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(item => item.Content).HasColumnType("varbinary(max)");
        builder.Property(item => item.ContentLengthBytes).HasColumnType("bigint");
        builder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.Status,
            item.LastPublishedAtUtc,
            item.CreatedAtUtc,
        });
    }
}
