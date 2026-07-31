using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", "tenancy");
        builder.ConfigureAuditableEntity<Branch, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Code)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.ContactEmail).HasMaxLength(320);
        builder.Property(entity => entity.ContactPhoneNumber).HasMaxLength(40);
        builder.OwnsOne(entity => entity.Address, address =>
            address.ConfigureAddress("Address"));
        builder.HasIndex(entity => new { entity.TenantId, entity.Code }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Name }).IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.IsActive });
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
