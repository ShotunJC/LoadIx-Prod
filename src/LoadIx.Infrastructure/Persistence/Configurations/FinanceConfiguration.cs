using LoadIx.Domain.Customers;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Tenancy;
using LoadIx.Domain.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class TenantTaxConfigurationConfiguration :
    IEntityTypeConfiguration<TenantTaxConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantTaxConfiguration> builder)
    {
        builder.ToTable("TenantTaxConfigurations", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DefaultTaxTreatment)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.TaxRatePercent).HasPrecision(9, 4);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.TenantId).IsUnique();
    }
}

internal sealed class TripFinancialsConfiguration :
    IEntityTypeConfiguration<TripFinancials>
{
    public void Configure(EntityTypeBuilder<TripFinancials> builder)
    {
        builder.ToTable("TripFinancials", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TripNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ContractNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.CurrencyCode)
            .HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.RateType)
            .HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(entity => entity.FixedAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.BaseFee).HasPrecision(19, 4);
        builder.Property(entity => entity.PerTonneAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.MinimumCharge).HasPrecision(19, 4);
        builder.Property(entity => entity.FuelSurchargePercent).HasPrecision(9, 4);
        builder.Property(entity => entity.BillableTonnes).HasPrecision(18, 3);
        builder.Property(entity => entity.Subtotal).HasPrecision(19, 4);
        builder.Property(entity => entity.Surcharge).HasPrecision(19, 4);
        builder.Property(entity => entity.TaxExclusiveAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.TaxTreatment)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.TaxRatePercent).HasPrecision(9, 4);
        builder.Property(entity => entity.TaxAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.Total).HasPrecision(19, 4);
        builder.Property(entity => entity.CalculationVersion)
            .HasMaxLength(40).IsUnicode(false);
        builder.Property(entity => entity.ApprovalStatus)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.ApprovalReason).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.Ignore(entity => entity.TotalCost);
        builder.Ignore(entity => entity.GrossMargin);
        builder.Ignore(entity => entity.MarginPercentage);
        builder.Ignore(entity => entity.CostPerTonne);
        builder.Ignore(entity => entity.RevenuePerTonne);
        builder.Ignore(entity => entity.IsInvoiced);
        builder.HasIndex(entity => new { entity.TenantId, entity.TripId }).IsUnique();
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CalculatedAtUtc,
            entity.ApprovalStatus,
            entity.TripId,
        });
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.CustomerId,
            entity.TransportContractId,
            entity.ApprovalStatus,
            entity.InvoiceBatchId,
        });
        builder.HasIndex(entity => new { entity.TenantId, entity.InvoiceBatchId, entity.TripId })
            .IsUnique()
            .HasFilter("[InvoiceBatchId] IS NOT NULL");
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(entity => entity.TripId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CustomerId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entity => entity.Costs).WithOne()
            .HasForeignKey(entity => entity.TripFinancialsId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Costs)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CostEntryConfiguration : IEntityTypeConfiguration<CostEntry>
{
    public void Configure(EntityTypeBuilder<CostEntry> builder)
    {
        builder.ToTable("CostEntries", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.Type)
            .HasConversion<string>().HasMaxLength(40).IsUnicode(false);
        builder.Property(entity => entity.Amount).HasPrecision(19, 4);
        builder.Property(entity => entity.CurrencyCode)
            .HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TripFinancialsId,
            entity.OccurredAtUtc,
            entity.Id,
        });
    }
}

internal sealed class InvoiceBatchConfiguration :
    IEntityTypeConfiguration<InvoiceBatch>
{
    public void Configure(EntityTypeBuilder<InvoiceBatch> builder)
    {
        builder.ToTable("InvoiceBatches", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.BatchNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.CustomerName).HasMaxLength(200);
        builder.Property(entity => entity.ContractNumber)
            .HasMaxLength(80).IsUnicode(false);
        builder.Property(entity => entity.CurrencyCode)
            .HasMaxLength(3).IsUnicode(false);
        builder.Property(entity => entity.Status)
            .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.Ignore(entity => entity.TaxExclusiveAmount);
        builder.Ignore(entity => entity.TaxAmount);
        builder.Ignore(entity => entity.Total);
        builder.HasIndex(entity => new { entity.TenantId, entity.BatchNumber })
            .IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.CreatedAtUtc });
        builder.HasIndex(entity => new { entity.TenantId, entity.CustomerId, entity.CreatedAtUtc, entity.Id });
        builder.HasMany(entity => entity.Lines).WithOne()
            .HasForeignKey(entity => entity.InvoiceBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(entity => entity.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvoiceBatchLineConfiguration :
    IEntityTypeConfiguration<InvoiceBatchLine>
{
    public void Configure(EntityTypeBuilder<InvoiceBatchLine> builder)
    {
        builder.ToTable("InvoiceBatchLines", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TripNumber)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.BillableTonnes).HasPrecision(18, 3);
        builder.Property(entity => entity.CurrencyCode)
            .HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.TaxExclusiveAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.TaxTreatment)
            .HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(entity => entity.TaxAmount).HasPrecision(19, 4);
        builder.Property(entity => entity.Total).HasPrecision(19, 4);
        builder.HasIndex(entity => new { entity.TenantId, entity.TripFinancialsId })
            .IsUnique();
        builder.HasIndex(entity => new { entity.TenantId, entity.InvoiceBatchId, entity.TripNumber });
    }
}

internal sealed class FinancialApprovalConfiguration :
    IEntityTypeConfiguration<FinancialApproval>
{
    public void Configure(EntityTypeBuilder<FinancialApproval> builder)
    {
        builder.ToTable("FinancialApprovals", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.TargetType)
            .HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.Outcome)
            .HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(entity => entity.Reason).HasMaxLength(500);
        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.TargetType,
            entity.TargetId,
            entity.OccurredAtUtc,
        });
    }
}
