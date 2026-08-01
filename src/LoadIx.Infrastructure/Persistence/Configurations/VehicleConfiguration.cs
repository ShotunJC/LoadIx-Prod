using LoadIx.Domain.Fleet;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", "fleet");
        builder.ConfigureAuditableEntity<Vehicle, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Registration)
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.FleetNumber)
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(entity => entity.Make).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Model).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.RoadworthyExpiry).HasColumnType("date");
        builder.Property(entity => entity.LicenceExpiry).HasColumnType("date");
        builder.OwnsOne(entity => entity.TareWeight, weight =>
            weight.ConfigureWeight("TareWeight"));
        builder.OwnsOne(entity => entity.CarryingCapacity, weight =>
            weight.ConfigureWeight("CarryingCapacity"));
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Registration,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.FleetNumber,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.BranchId,
            entity.IsActive,
            entity.IsAvailable,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.IsActive,
            entity.LicenceExpiry,
            entity.RoadworthyExpiry,
        });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.BranchId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
