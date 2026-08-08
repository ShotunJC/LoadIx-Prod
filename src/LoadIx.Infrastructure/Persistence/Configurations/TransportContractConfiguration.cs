using LoadIx.Domain.Commercial;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class TransportContractConfiguration :
    IEntityTypeConfiguration<TransportContract>
{
    public void Configure(EntityTypeBuilder<TransportContract> builder)
    {
        builder.ToTable("TransportContracts", "commercial");
        builder.ConfigureAuditableEntity<TransportContract, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.ContractNumber)
            .HasMaxLength(60).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.DefaultCurrency)
            .HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.WeightTolerancePercent)
            .HasPrecision(9, 4);
        builder.Property(entity => entity.Notes).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.TenantId, entity.ContractNumber })
            .IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.Status,
            entity.StartDate,
            entity.Id,
        });
        builder.HasIndex(entity => new { entity.TenantId, entity.CustomerId, entity.Status });
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CustomerId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Routes).WithOne()
            .HasForeignKey(entity => entity.TransportContractId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Rates).WithOne()
            .HasForeignKey(entity => entity.TransportContractId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Commodities).WithOne()
            .HasForeignKey(entity => entity.TransportContractId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Routes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(entity => entity.Rates)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(entity => entity.Commodities)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ContractRouteConfiguration :
    IEntityTypeConfiguration<ContractRoute>
{
    public void Configure(EntityTypeBuilder<ContractRoute> builder)
    {
        builder.ToTable("ContractRoutes", "commercial");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Reference)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.CollectionInstructions).HasMaxLength(1000);
        builder.Property(entity => entity.DeliveryInstructions).HasMaxLength(1000);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TransportContractId,
            entity.Reference,
        });
        builder.HasOne<Location>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.SourceLocationId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.DestinationLocationId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContractCommodityConfiguration :
    IEntityTypeConfiguration<ContractCommodity>
{
    public void Configure(EntityTypeBuilder<ContractCommodity> builder)
    {
        builder.ToTable("ContractCommodities", "commercial");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TransportContractId,
            entity.CommodityId,
        });
        builder.HasOne<Commodity>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CommodityId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ContractRateConfiguration :
    IEntityTypeConfiguration<ContractRate>
{
    public void Configure(EntityTypeBuilder<ContractRate> builder)
    {
        builder.ToTable("ContractRates", "commercial");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.RateType)
            .HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(entity => entity.FixedAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.BaseFee).HasPrecision(19, 4);
        builder.Property(entity => entity.PerTonneAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.MinimumCharge).HasPrecision(19, 4);
        builder.Property(entity => entity.FuelSurchargePercent).HasPrecision(9, 4);
        builder.Property(entity => entity.RoundingMode)
            .HasConversion<string>().HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.ContractRouteId,
            entity.EffectiveFrom,
            entity.Id,
        });
        builder.HasOne<ContractRoute>().WithMany()
            .HasForeignKey(entity => entity.ContractRouteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
