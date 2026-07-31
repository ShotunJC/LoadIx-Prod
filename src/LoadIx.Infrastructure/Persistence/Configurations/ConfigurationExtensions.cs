using LoadIx.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static void ConfigureAuditableEntity<TEntity, TId>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity<TId>
        where TId : notnull
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CreatedAtUtc)
            .HasPrecision(7)
            .IsRequired();
        builder.Property(entity => entity.ModifiedAtUtc)
            .HasPrecision(7)
            .IsRequired();
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }

    public static PropertyBuilder<TenantId> ConfigureTenantId(
        this PropertyBuilder<TenantId> property) =>
        property
            .HasConversion(
                tenantId => tenantId.Value,
                value => TenantId.From(value))
            .ValueGeneratedNever();

    public static PropertyBuilder<TenantId?> ConfigureNullableTenantId(
        this PropertyBuilder<TenantId?> property) =>
        property.HasConversion(new ValueConverter<TenantId?, Guid?>(
            tenantId => tenantId.HasValue
                ? tenantId.Value.Value
                : null,
            value => value.HasValue
                ? TenantId.From(value.Value)
                : null));

    public static void ConfigureAddress<TEntity>(
        this OwnedNavigationBuilder<TEntity, Address> address,
        string prefix)
        where TEntity : class
    {
        address.Property(value => value.Line1)
            .HasColumnName($"{prefix}Line1")
            .HasMaxLength(200)
            .IsRequired();
        address.Property(value => value.Line2)
            .HasColumnName($"{prefix}Line2")
            .HasMaxLength(200);
        address.Property(value => value.City)
            .HasColumnName($"{prefix}City")
            .HasMaxLength(120)
            .IsRequired();
        address.Property(value => value.ProvinceOrRegion)
            .HasColumnName($"{prefix}ProvinceOrRegion")
            .HasMaxLength(120)
            .IsRequired();
        address.Property(value => value.PostalCode)
            .HasColumnName($"{prefix}PostalCode")
            .HasMaxLength(20)
            .IsRequired();
        address.Property(value => value.CountryCode)
            .HasColumnName($"{prefix}CountryCode")
            .HasMaxLength(2)
            .IsUnicode(false)
            .IsRequired();
    }

    public static void ConfigureWeight<TEntity>(
        this OwnedNavigationBuilder<TEntity, Weight> weight,
        string prefix)
        where TEntity : class
    {
        weight.Property(value => value.Value)
            .HasColumnName($"{prefix}Value")
            .HasPrecision(18, 3)
            .IsRequired();
        weight.Property(value => value.Unit)
            .HasColumnName($"{prefix}Unit")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
    }

    public static void ConfigureMoney<TEntity>(
        this OwnedNavigationBuilder<TEntity, Money> money,
        string prefix)
        where TEntity : class
    {
        money.Property(value => value.Amount)
            .HasColumnName($"{prefix}Amount")
            .HasPrecision(19, 4)
            .IsRequired();
        money.Property(value => value.CurrencyCode)
            .HasColumnName($"{prefix}CurrencyCode")
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();
    }
}
