using LoadIx.Domain.Customers;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations", "locations");
        builder.ConfigureAuditableEntity<Location, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Code)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Type)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.Property(entity => entity.Latitude).HasPrecision(9, 6);
        builder.Property(entity => entity.Longitude).HasPrecision(9, 6);
        builder.Property(entity => entity.GeofenceRadiusMetres).HasPrecision(10, 2);
        builder.Property(entity => entity.OperatingInstructions).HasMaxLength(2000);
        builder.Property(entity => entity.ContactName).HasMaxLength(160);
        builder.Property(entity => entity.ContactPhoneNumber).HasMaxLength(40);
        builder.Property(entity => entity.OperatingHours).HasMaxLength(500);
        builder.OwnsOne(entity => entity.PhysicalAddress, address =>
            address.ConfigureAddress("Physical"));
        builder.HasIndex(entity => new { entity.TenantId, entity.Code }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Type,
            entity.IsActive,
        });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CustomerId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
