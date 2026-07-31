using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "tenancy");
        builder.ConfigureAuditableEntity<Tenant, Domain.Common.TenantId>();
        builder.Property(entity => entity.Id).ConfigureTenantId();
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RegistrationNumber)
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(entity => entity.TaxNumber).HasMaxLength(80);
        builder.Property(entity => entity.DefaultCurrency)
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.TimeZone).HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => entity.RegistrationNumber).IsUnique();
        builder.HasIndex(entity => entity.Name).IsUnique();
        builder.HasIndex(entity => entity.IsActive);
    }
}
