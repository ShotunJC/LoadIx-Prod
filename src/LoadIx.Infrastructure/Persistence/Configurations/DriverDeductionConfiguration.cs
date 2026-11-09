using LoadIx.Domain.Fleet;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class DriverDeductionConfiguration : IEntityTypeConfiguration<DriverDeduction>
{
    public void Configure(EntityTypeBuilder<DriverDeduction> builder)
    {
        builder.ToTable("DriverDeductions", "fleet");
        builder.ConfigureAuditableEntity<DriverDeduction, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DeductionNumber).HasMaxLength(60).IsRequired();
        builder.Property(entity => entity.Category)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.Property(entity => entity.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.TotalAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(entity => entity.RemainingBalance).HasPrecision(18, 2);
        builder.Property(entity => entity.EffectiveDate).HasColumnType("date");
        builder.Property(entity => entity.Description).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.ApprovedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.CancelledAtUtc).HasPrecision(7);
        builder.Property(entity => entity.CancellationReason).HasMaxLength(500);

        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.DeductionNumber,
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
