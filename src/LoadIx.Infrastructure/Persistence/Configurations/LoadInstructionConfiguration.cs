using LoadIx.Domain.Commercial;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Tenancy;
using LoadIx.Infrastructure.Instructions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class LoadInstructionConfiguration :
    IEntityTypeConfiguration<LoadInstruction>
{
    public void Configure(EntityTypeBuilder<LoadInstruction> builder)
    {
        builder.ToTable("LoadInstructions", "instructions");
        builder.ConfigureAuditableEntity<LoadInstruction, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.InstructionNumber)
            .HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.RequestedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.QuantityUnit)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.Priority)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.CustomerPurchaseOrderReference)
            .HasMaxLength(100);
        builder.Property(entity => entity.CustomerInstructionReference)
            .HasMaxLength(100);
        builder.Property(entity => entity.SpecialInstructions).HasMaxLength(4000);
        builder.Property(entity => entity.CancellationReason).HasMaxLength(500);
        builder.Ignore(entity => entity.CompletedQuantity);
        builder.Ignore(entity => entity.RemainingQuantity);
        builder.Ignore(entity => entity.OverDeliveredQuantity);
        builder.Ignore(entity => entity.UnderDeliveredQuantity);
        builder.HasIndex(entity => new { entity.TenantId, entity.InstructionNumber })
            .IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Status,
            entity.CollectionWindowStartUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CustomerId,
            entity.Status,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TransportContractId,
            entity.Status,
        });
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CustomerId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TransportContract>().WithMany()
            .HasForeignKey(entity => entity.TransportContractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContractRoute>().WithMany()
            .HasForeignKey(entity => entity.ContractRouteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Commodity>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CommodityId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Trips).WithOne()
            .HasForeignKey(entity => entity.LoadInstructionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Trips)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class GeneratedTripConfiguration :
    IEntityTypeConfiguration<GeneratedTrip>
{
    public void Configure(EntityTypeBuilder<GeneratedTrip> builder)
    {
        builder.ToTable("GeneratedTrips", "instructions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TripNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.PlannedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.CompletedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.QuantityUnit)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(entity => new { entity.TenantId, entity.TripNumber })
            .IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.LoadInstructionId,
            entity.Status,
            entity.Id,
        });
    }
}

internal sealed class TripGenerationOperationConfiguration :
    IEntityTypeConfiguration<TripGenerationOperation>
{
    public void Configure(EntityTypeBuilder<TripGenerationOperation> builder)
    {
        builder.ToTable("TripGenerationOperations", "instructions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.IdempotencyKey)
            .HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.RequestHash)
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.LoadInstructionId,
            entity.IdempotencyKey,
        }).IsUnique();
        builder.HasOne<LoadInstruction>().WithMany()
            .HasForeignKey(entity => entity.LoadInstructionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
