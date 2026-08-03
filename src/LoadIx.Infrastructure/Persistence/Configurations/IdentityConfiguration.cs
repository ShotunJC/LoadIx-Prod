using LoadIx.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", "identity");
        builder.Property(user => user.TenantId).ConfigureNullableTenantId();
        builder.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
        builder.HasIndex(user => new { user.TenantId, user.NormalizedEmail }).IsUnique();
        builder.HasIndex(user => new { user.TenantId, user.IsActive });
    }
}

internal sealed class ApplicationRoleConfiguration
    : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", "identity");
        builder.Property(role => role.TenantId).ConfigureTenantId();
        builder.Property(role => role.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(role => role.Permissions).HasMaxLength(1000).IsRequired();
        builder.HasIndex(role => new { role.TenantId, role.DisplayName }).IsUnique();
    }
}

internal sealed class RefreshSessionConfiguration
    : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions", "identity");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TenantId).ConfigureNullableTenantId();
        builder.Property(session => session.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(session => session.UserAgent).HasMaxLength(300);
        builder.Property(session => session.IpAddress)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new
        {
            session.TenantId,
            session.UserId,
            session.RevokedAtUtc,
        });
        builder.HasIndex(session => session.FamilyId);
        builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserInvitationConfiguration
    : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("UserInvitations", "identity");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.TenantId).ConfigureTenantId();
        builder.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail)
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(invitation => invitation.DisplayName)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(invitation => invitation.RoleIds)
            .HasMaxLength(2000)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new
        {
            invitation.TenantId,
            invitation.NormalizedEmail,
            invitation.AcceptedAtUtc,
            invitation.RevokedAtUtc,
        });
    }
}

internal sealed class SecurityAuditEventConfiguration
    : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents", "audit");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.TenantId).ConfigureNullableTenantId();
        builder.Property(auditEvent => auditEvent.EventType)
            .HasMaxLength(120)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(auditEvent => auditEvent.Outcome)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(auditEvent => auditEvent.TargetType)
            .HasMaxLength(80)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(auditEvent => auditEvent.TargetId).HasMaxLength(160);
        builder.Property(auditEvent => auditEvent.ActorCredentialId);
        builder.Property(auditEvent => auditEvent.IpAddress)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(auditEvent => auditEvent.CorrelationId)
            .HasMaxLength(128)
            .IsUnicode(false);
        builder.Property(auditEvent => auditEvent.FailureCode)
            .HasMaxLength(160)
            .IsUnicode(false);
        builder.Property(auditEvent => auditEvent.TraceId)
            .HasMaxLength(200);
        builder.Property(auditEvent => auditEvent.NonceHash)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(auditEvent => auditEvent.IdempotencyKeyHash)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(auditEvent => auditEvent.RequestMetadataJson)
            .HasColumnType("nvarchar(4000)")
            .HasMaxLength(4_000);
        builder.Property(auditEvent => auditEvent.BeforeSummaryJson)
            .HasColumnType("nvarchar(4000)")
            .HasMaxLength(4_000);
        builder.Property(auditEvent => auditEvent.AfterSummaryJson)
            .HasColumnType("nvarchar(4000)")
            .HasMaxLength(4_000);
        builder.Property(auditEvent => auditEvent.Reason)
            .HasMaxLength(500);
        builder.HasIndex(auditEvent => new
        {
            auditEvent.TenantId,
            auditEvent.OccurredAtUtc,
        });
        builder.HasIndex(auditEvent => new
        {
            auditEvent.ActorUserId,
            auditEvent.OccurredAtUtc,
        });
        builder.HasIndex(auditEvent => new
        {
            auditEvent.TenantId,
            auditEvent.ActorDeviceId,
            auditEvent.OccurredAtUtc,
        });
    }
}
