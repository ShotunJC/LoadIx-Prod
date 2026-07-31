using LoadIx.Domain.Customers;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "customers");
        builder.ConfigureAuditableEntity<Customer, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.TradingName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RegistrationNumber)
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(entity => entity.TaxNumber).HasMaxLength(80);
        builder.Property(entity => entity.Notes).HasMaxLength(4000);
        builder.OwnsOne(entity => entity.BillingAddress, address =>
            address.ConfigureAddress("Billing"));
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.RegistrationNumber,
        }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.LegalName })
            .IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.IsActive });
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Contacts)
            .WithOne()
            .HasForeignKey(entity => new { entity.TenantId, entity.CustomerId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Contacts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
