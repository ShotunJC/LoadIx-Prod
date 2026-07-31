using LoadIx.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoadIx.Infrastructure.Persistence.Configurations;

internal sealed class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("NotificationMessages", "notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.Type).HasMaxLength(80).IsUnicode(false);
        builder.Property(item => item.Title).HasMaxLength(200);
        builder.Property(item => item.Body).HasMaxLength(2000);
        builder.Property(item => item.DeduplicationKey).HasMaxLength(180).IsUnicode(false);
        builder.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.DeduplicationKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.ReadAtUtc, item.CreatedAtUtc });
    }
}

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries", "notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.Channel).HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.Status).HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.ProviderCorrelationId).HasMaxLength(160);
        builder.Property(item => item.Failure).HasMaxLength(1000);
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc, item.Id });
        builder.HasOne<NotificationMessage>().WithMany().HasForeignKey(item => item.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences", "notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.NotificationType).HasMaxLength(80).IsUnicode(false);
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.NotificationType }).IsUnique();
    }
}

internal sealed class TenantNotificationTemplateConfiguration : IEntityTypeConfiguration<TenantNotificationTemplate>
{
    public void Configure(EntityTypeBuilder<TenantNotificationTemplate> builder)
    {
        builder.ToTable("TenantNotificationTemplates", "notifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).ConfigureTenantId();
        builder.Property(item => item.NotificationType).HasMaxLength(80).IsUnicode(false);
        builder.Property(item => item.Channel).HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.SubjectTemplate).HasMaxLength(300);
        builder.Property(item => item.BodyTemplate).HasMaxLength(4000);
        builder.HasIndex(item => new { item.TenantId, item.NotificationType, item.Channel }).IsUnique();
    }
}
