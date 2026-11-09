using LoadIx.Domain.Fleet;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class DriverWarningConfiguration : IEntityTypeConfiguration<DriverWarning>
{
    public void Configure(EntityTypeBuilder<DriverWarning> builder)
    {
        builder.ToTable("DriverWarnings", "fleet");
        builder.ConfigureAuditableEntity<DriverWarning, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.WarningNumber).HasMaxLength(60).IsRequired();
        builder.Property(entity => entity.WarningType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(entity => entity.Category)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.IncidentDateUtc).HasPrecision(7);
        builder.Property(entity => entity.IssuedDateUtc).HasPrecision(7);
        builder.Property(entity => entity.ExpiryDateUtc).HasPrecision(7);
        builder.Property(entity => entity.Description).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.RevokedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.RevocationReason).HasMaxLength(500);

        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.WarningNumber,
        }).IsUnique();

        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.DriverId,
            entity.Status,
        });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.DriverId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
