using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LoadIx.Infrastructure.Persistence.Configurations;

namespace LoadIx.Infrastructure.Governance;

internal sealed class TenantRetentionPolicyConfiguration
    : IEntityTypeConfiguration<TenantRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<TenantRetentionPolicy> builder)
    {
        builder.ToTable("TenantRetentionPolicies", "governance");
        builder.ConfigureAuditableEntity<TenantRetentionPolicy, Guid>();
        builder.Property(policy => policy.TenantId).ConfigureTenantId();
        builder.Property(policy => policy.LegalHoldReason).HasMaxLength(500);
        builder.HasIndex(policy => policy.TenantId).IsUnique();
    }
}

internal sealed class AnonymisationRequestConfiguration
    : IEntityTypeConfiguration<AnonymisationRequest>
{
    public void Configure(EntityTypeBuilder<AnonymisationRequest> builder)
    {
        builder.ToTable("AnonymisationRequests", "governance");
        builder.ConfigureAuditableEntity<AnonymisationRequest, Guid>();
        builder.Property(request => request.TenantId).ConfigureTenantId();
        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsUnicode(false);
        builder.Property(request => request.Reason).HasMaxLength(500).IsRequired();
        builder.Property(request => request.ReviewReason).HasMaxLength(500);
        builder.HasIndex(request => new
        {
            request.TenantId,
            request.UserId,
            request.Status,
        });
    }
}
