using LoadIx.Domain.Documents;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration :
    IEntityTypeConfiguration<OperationalDocument>
{
    public void Configure(EntityTypeBuilder<OperationalDocument> builder)
    {
        builder.ToTable("Documents", "documents");
        builder.ConfigureAuditableEntity<OperationalDocument, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.OwnerType)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.DocumentType)
            .HasConversion<string>().HasMaxLength(48).IsUnicode(false);
        builder.Property(entity => entity.DisplayFileName)
            .HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.ContentType)
            .HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.Sha256Checksum)
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.StorageKey)
            .HasMaxLength(500).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.PreviewStorageKey)
            .HasMaxLength(500).IsUnicode(false);
        builder.Property(entity => entity.UploadedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.DeletedAtUtc).HasPrecision(7);

        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.OwnerType,
            entity.OwnerId,
            entity.Status,
            entity.UploadedAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.OwnerType,
            entity.OwnerId,
            entity.DocumentType,
            entity.Sha256Checksum,
        });
        builder.HasIndex(entity => new { entity.TenantId, entity.StorageKey })
            .IsUnique();
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentUploadOperationConfiguration :
    IEntityTypeConfiguration<DocumentUploadOperation>
{
    public void Configure(EntityTypeBuilder<DocumentUploadOperation> builder)
    {
        builder.ToTable("UploadOperations", "documents");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.IdempotencyKey)
            .HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.RequestHash)
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasPrecision(7);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.ActorUserId,
            entity.IdempotencyKey,
        }).IsUnique();
    }
}
