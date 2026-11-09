using LoadIx.Domain.Finance;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class PayrollBatchConfiguration : IEntityTypeConfiguration<PayrollBatch>
{
    public void Configure(EntityTypeBuilder<PayrollBatch> builder)
    {
        builder.ToTable("PayrollBatches", "finance");
        builder.HasKey(entity => entity.Id);
        builder.ConfigureAuditableEntity<PayrollBatch, Guid>();
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.BatchNumber)
            .HasMaxLength(60)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.PeriodStartDate).HasColumnType("date");
        builder.Property(entity => entity.PeriodEndDate).HasColumnType("date");
        builder.Property(entity => entity.Frequency)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.CurrencyCode)
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false);
        builder.Property(entity => entity.ApprovedAtUtc).HasPrecision(7);
        builder.Property(entity => entity.ExportedAtUtc).HasPrecision(7);
        builder.Ignore(entity => entity.TotalDeductionsAmount);

        builder.HasIndex(entity => new { entity.TenantId, entity.BatchNumber })
            .IsUnique();

        builder.HasIndex(entity => new { entity.TenantId, entity.Status, entity.PeriodStartDate });

        builder.HasMany(entity => entity.Lines)
            .WithOne()
            .HasForeignKey(entity => entity.PayrollBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(entity => entity.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayrollBatchLineConfiguration : IEntityTypeConfiguration<PayrollBatchLine>
{
    public void Configure(EntityTypeBuilder<PayrollBatchLine> builder)
    {
        builder.ToTable("PayrollBatchLines", "finance");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.TenantId).ConfigureTenantId();
        builder.Property(entity => entity.DriverEmployeeNumber)
            .HasMaxLength(50)
            .IsUnicode(false);
        builder.Property(entity => entity.DriverName)
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(entity => entity.TransactionCode)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(entity => entity.Description)
            .HasMaxLength(300)
            .IsRequired();
        builder.Property(entity => entity.Amount).HasPrecision(18, 2);
        builder.Property(entity => entity.BalanceAfterDeduction).HasPrecision(18, 2);

        builder.HasIndex(entity => new
        {
            entity.TenantId,
            entity.PayrollBatchId,
            entity.DriverId,
        });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
