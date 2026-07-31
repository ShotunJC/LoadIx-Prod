using LoadIx.Domain.Fleet;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers", "fleet");
        builder.ConfigureAuditableEntity<Driver, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.LastName).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.IdentificationReference)
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(entity => entity.MobileNumber).HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.LicenceNumber).HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.LicenceExpiry).HasColumnType("date");
        builder.Property(entity => entity.EmergencyContactName)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(entity => entity.EmergencyContactNumber)
            .HasMaxLength(40)
            .IsRequired();
        builder.Ignore(entity => entity.DisplayName);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.IdentificationReference,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.LicenceNumber,
        }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.UserId,
        }).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.BranchId,
            entity.IsActive,
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
