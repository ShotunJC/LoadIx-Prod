using LoadIx.Domain.Commercial;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class CommodityConfiguration : IEntityTypeConfiguration<Commodity>
{
    public void Configure(EntityTypeBuilder<Commodity> builder)
    {
        builder.ToTable("Commodities", "commercial");
        builder.ConfigureAuditableEntity<Commodity, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Code)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.DefaultWeightUnit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.HasIndex(entity => new { entity.TenantId, entity.Code }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.IsActive });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
