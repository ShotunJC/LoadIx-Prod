using LoadIx.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class ContactPersonConfiguration :
    IEntityTypeConfiguration<ContactPerson>
{
    public void Configure(EntityTypeBuilder<ContactPerson> builder)
    {
        builder.ToTable("ContactPeople", "customers");
        builder.ConfigureAuditableEntity<ContactPerson, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(320);
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(40);
        builder.Property(entity => entity.Category)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsUnicode(false);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CustomerId,
            entity.Name,
        });
        builder.HasIndex(entity => new { entity.TenantId, entity.IsActive });
    }
}
