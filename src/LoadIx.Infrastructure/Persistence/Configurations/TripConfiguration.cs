using LoadIx.Domain.Commercial;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Fleet;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Tenancy;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips", "trips");
        builder.ConfigureAuditableEntity<Trip, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TripNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.ContractNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.ContractVersion)
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.RouteReference)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.PlannedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.QuantityUnit)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.TrailerIds)
            .HasMaxLength(110).IsUnicode(false);
        builder.Property(entity => entity.OperationalNotes).HasMaxLength(4000);
        builder.Property(entity => entity.CancellationOrRejectionReason)
            .HasMaxLength(500);
        builder.Property(entity => entity.SourceGrossWeight).HasPrecision(18, 3);
        builder.Property(entity => entity.SourceTareWeight).HasPrecision(18, 3);
        builder.Property(entity => entity.SourceWeighbridgeReference)
            .HasMaxLength(120);
        builder.Property(entity => entity.DestinationGrossWeight)
            .HasPrecision(18, 3);
        builder.Property(entity => entity.DestinationTareWeight)
            .HasPrecision(18, 3);
        builder.Property(entity => entity.DestinationWeighbridgeReference)
            .HasMaxLength(120);
        builder.Ignore(entity => entity.CurrentTrailerIds);
        builder.Ignore(entity => entity.SourceNetWeight);
        builder.Ignore(entity => entity.DestinationNetWeight);

        builder.OwnsOne(entity => entity.RateSnapshot, rate =>
        {
            rate.Property(value => value.RateId).HasColumnName("RateId");
            rate.Property(value => value.RateType)
                .HasColumnName("RateType")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsUnicode(false);
            rate.Property(value => value.EffectiveFrom)
                .HasColumnName("RateEffectiveFrom");
            rate.Property(value => value.EffectiveTo)
                .HasColumnName("RateEffectiveTo");
            rate.Property(value => value.FixedAmount)
                .HasColumnName("RateFixedAmount").HasPrecision(19, 4);
            rate.Property(value => value.BaseFee)
                .HasColumnName("RateBaseFee").HasPrecision(19, 4);
            rate.Property(value => value.PerTonneAmount)
                .HasColumnName("RatePerTonneAmount").HasPrecision(19, 4);
            rate.Property(value => value.MinimumCharge)
                .HasColumnName("RateMinimumCharge").HasPrecision(19, 4);
            rate.Property(value => value.FuelSurchargePercent)
                .HasColumnName("RateFuelSurchargePercent").HasPrecision(9, 4);
            rate.Property(value => value.RoundingDecimalPlaces)
                .HasColumnName("RateRoundingDecimalPlaces");
            rate.Property(value => value.RoundingMode)
                .HasColumnName("RateRoundingMode")
                .HasConversion<string>()
                .HasMaxLength(24)
                .IsUnicode(false);
            rate.Property(value => value.CurrencyCode)
                .HasColumnName("RateCurrencyCode")
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        builder.HasIndex(entity => new { entity.TenantId, entity.TripNumber })
            .IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.LoadInstructionId });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Status,
            entity.PlannedCollectionAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Status,
            entity.PlannedDeliveryAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.PlannedDeliveryAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CompletedAtUtc,
            entity.Status,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CustomerId,
            entity.CompletedAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CustomerId,
            entity.PlannedDeliveryAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.DriverId,
            entity.PlannedCollectionAtUtc,
            entity.PlannedDeliveryAtUtc,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.VehicleId,
            entity.PlannedCollectionAtUtc,
            entity.PlannedDeliveryAtUtc,
        });
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoadInstruction>().WithMany()
            .HasForeignKey(entity => entity.LoadInstructionId)
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
        builder.HasOne<Driver>().WithMany()
            .HasForeignKey(entity => entity.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(entity => entity.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Transitions).WithOne()
            .HasForeignKey(entity => entity.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Transitions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(entity => entity.Assignments).WithOne()
            .HasForeignKey(entity => entity.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Assignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TripTransitionConfiguration :
    IEntityTypeConfiguration<TripTransition>
{
    public void Configure(EntityTypeBuilder<TripTransition> builder)
    {
        builder.ToTable("TripTransitions", "trips");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.PreviousStatus)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.NewStatus)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.OccurredAtUtc).HasPrecision(7);
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TripId,
            entity.OccurredAtUtc,
            entity.Id,
        });
    }
}

internal sealed class TripAssignmentConfiguration :
    IEntityTypeConfiguration<TripAssignment>
{
    public void Configure(EntityTypeBuilder<TripAssignment> builder)
    {
        builder.ToTable("TripAssignments", "trips");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TrailerIds)
            .HasMaxLength(110).IsUnicode(false);
        builder.Property(entity => entity.AssignedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.EndedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TripId,
            entity.AssignedAtUtc,
            entity.Id,
        });
    }
}

internal sealed class TripCommandOperationConfiguration :
    IEntityTypeConfiguration<TripCommandOperation>
{
    public void Configure(EntityTypeBuilder<TripCommandOperation> builder)
    {
        builder.ToTable("TripCommandOperations", "trips");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Command)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
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

internal sealed class TripLocationUpdateConfiguration :
    IEntityTypeConfiguration<TripLocationUpdate>
{
    public void Configure(EntityTypeBuilder<TripLocationUpdate> builder)
    {
        builder.ToTable("TripLocationUpdates", "trips");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Latitude).HasPrecision(9, 6);
        builder.Property(entity => entity.Longitude).HasPrecision(9, 6);
        builder.Property(entity => entity.AccuracyMetres).HasPrecision(10, 2);
        builder.Property(entity => entity.CapturedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ReceivedAtUtc).HasPrecision(7);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TripId,
            entity.CapturedAtUtc,
            entity.Id,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CapturedAtUtc,
            entity.TripId,
            entity.Id,
        });
        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(entity => entity.TripId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
