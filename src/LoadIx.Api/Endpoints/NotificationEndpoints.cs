using LoadIx.Application.Notifications;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();
        group.MapGet("/", async (LoadIxDbContext db, CurrentRequestContext context, CancellationToken cancellationToken) =>
        {
            var items = await db.NotificationMessages.AsNoTracking()
                .Where(item => item.RecipientUserId == context.UserId)
                .OrderByDescending(item => item.CreatedAtUtc).Take(100)
                .Select(item => new { item.Id, item.Type, item.Title, item.Body, item.CreatedAtUtc, item.ReadAtUtc })
                .ToListAsync(cancellationToken);
            return Results.Ok(new { items, unreadCount = items.Count(item => item.ReadAtUtc == null) });
        });
        group.MapGet("/types", () => Results.Ok(NotificationTypes.All));
        group.MapPost("/{id:guid}/read", async (Guid id, LoadIxDbContext db, CurrentRequestContext context, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var item = await db.NotificationMessages.SingleOrDefaultAsync(item => item.Id == id && item.RecipientUserId == context.UserId, cancellationToken);
            if (item is null) return Results.NotFound();
            item.ReadAtUtc = time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
        group.MapPost("/read-all", async (LoadIxDbContext db, CurrentRequestContext context, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var unread = await db.NotificationMessages.Where(item => item.RecipientUserId == context.UserId && item.ReadAtUtc == null).ToListAsync(cancellationToken);
            unread.ForEach(item => item.ReadAtUtc = time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
        group.MapGet("/preferences", async (LoadIxDbContext db, CurrentRequestContext context, CancellationToken cancellationToken) =>
        {
            var saved = await db.UserNotificationPreferences.AsNoTracking()
                .Where(item => item.UserId == context.UserId)
                .ToDictionaryAsync(item => item.NotificationType, cancellationToken);
            return Results.Ok(NotificationTypes.All.Select(type =>
            {
                saved.TryGetValue(type, out var preference);
                return new NotificationPreferenceResponse(
                    type,
                    preference?.InAppEnabled ?? true,
                    preference?.EmailEnabled ?? true,
                    preference?.SmsEnabled ?? false);
            }));
        });
        group.MapPut("/preferences", async (
            NotificationPreferenceRequest request,
            LoadIxDbContext db,
            CurrentRequestContext context,
            CancellationToken cancellationToken) =>
        {
            if (!NotificationTypes.All.Contains(request.NotificationType, StringComparer.Ordinal))
                return Results.BadRequest(new { error = "notification.type_invalid" });
            var preference = await db.UserNotificationPreferences.SingleOrDefaultAsync(
                item => item.UserId == context.UserId && item.NotificationType == request.NotificationType,
                cancellationToken);
            if (preference is null)
            {
                db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId!.Value,
                    UserId = context.UserId!.Value,
                    NotificationType = request.NotificationType,
                    InAppEnabled = request.InAppEnabled,
                    EmailEnabled = request.EmailEnabled,
                    SmsEnabled = request.SmsEnabled,
                });
            }
            else
            {
                preference.InAppEnabled = request.InAppEnabled;
                preference.EmailEnabled = request.EmailEnabled;
                preference.SmsEnabled = request.SmsEnabled;
            }
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        var templates = group.MapGroup("/templates")
            .RequireAuthorization(LoadIxPermissions.TenantManage);
        templates.MapGet("/", async (LoadIxDbContext db, CancellationToken cancellationToken) =>
        {
            var items = await db.TenantNotificationTemplates.AsNoTracking()
                .OrderBy(item => item.NotificationType).ThenBy(item => item.Channel)
                .Select(item => new NotificationTemplateResponse(
                    item.Id,
                    item.NotificationType,
                    item.Channel,
                    item.SubjectTemplate,
                    item.BodyTemplate,
                    item.ModifiedAtUtc))
                .ToListAsync(cancellationToken);
            return Results.Ok(items);
        });
        templates.MapPut("/", async (
            NotificationTemplateRequest request,
            LoadIxDbContext db,
            CurrentRequestContext context,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            if (!NotificationTypes.All.Contains(request.NotificationType, StringComparer.Ordinal)
                || !ValidChannel(request.Channel))
            {
                return Results.BadRequest(new { error = "notification.template_invalid" });
            }
            var template = await db.TenantNotificationTemplates.SingleOrDefaultAsync(
                item => item.NotificationType == request.NotificationType && item.Channel == request.Channel,
                cancellationToken);
            if (template is null)
            {
                db.TenantNotificationTemplates.Add(new TenantNotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId!.Value,
                    NotificationType = request.NotificationType,
                    Channel = request.Channel,
                    SubjectTemplate = request.SubjectTemplate.Trim(),
                    BodyTemplate = request.BodyTemplate.Trim(),
                    ModifiedAtUtc = time.GetUtcNow().ToUniversalTime(),
                });
            }
            else
            {
                template.SubjectTemplate = request.SubjectTemplate.Trim();
                template.BodyTemplate = request.BodyTemplate.Trim();
                template.ModifiedAtUtc = time.GetUtcNow().ToUniversalTime();
            }
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
        templates.MapPost("/preview", (NotificationTemplateRequest request) =>
        {
            var sample = new NotificationMessage
            {
                Type = request.NotificationType,
                Title = "Trip LX-00042 assigned",
                Body = "LX-00042 is ready for dispatch review.",
            };
            return Results.Ok(new
            {
                subject = Render(request.SubjectTemplate, sample.Title, sample),
                body = Render(request.BodyTemplate, sample.Body, sample),
            });
        });

        group.MapGet("/failed", async (LoadIxDbContext db, CancellationToken cancellationToken) =>
        {
            var items = await db.NotificationDeliveries.AsNoTracking()
                .Where(item => item.Status == "Failed")
                .OrderByDescending(item => item.NextAttemptAtUtc).ThenBy(item => item.Id)
                .Take(100)
                .Join(db.NotificationMessages.AsNoTracking(),
                    delivery => delivery.NotificationId,
                    message => message.Id,
                    (delivery, message) => new FailedNotificationResponse(
                        delivery.Id,
                        message.Type,
                        delivery.Channel,
                        delivery.Attempts,
                        delivery.ProviderCorrelationId,
                        delivery.Failure,
                        delivery.NextAttemptAtUtc))
                .ToListAsync(cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization(LoadIxPermissions.AuditView);
        group.MapPost("/failed/{deliveryId:guid}/retry", async (
            Guid deliveryId,
            LoadIxDbContext db,
            TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var delivery = await db.NotificationDeliveries.SingleOrDefaultAsync(
                item => item.Id == deliveryId && item.Status == "Failed",
                cancellationToken);
            if (delivery is null) return Results.NotFound();
            delivery.Status = "Pending";
            delivery.NextAttemptAtUtc = time.GetUtcNow().ToUniversalTime();
            delivery.Failure = null;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(LoadIxPermissions.AuditView);
        return group;
    }

    private static bool ValidChannel(string channel) =>
        channel is "in-app" or "email" or "sms";

    private static string Render(string template, string fallback, NotificationMessage message) =>
        string.IsNullOrWhiteSpace(template)
            ? fallback
            : template
                .Replace("{{Title}}", message.Title, StringComparison.Ordinal)
                .Replace("{{Body}}", message.Body, StringComparison.Ordinal)
                .Replace("{{Type}}", message.Type, StringComparison.Ordinal);
}

public sealed record NotificationPreferenceResponse(
    string NotificationType,
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmsEnabled);

public sealed record NotificationPreferenceRequest(
    string NotificationType,
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmsEnabled);

public sealed record NotificationTemplateResponse(
    Guid Id,
    string NotificationType,
    string Channel,
    string SubjectTemplate,
    string BodyTemplate,
    DateTimeOffset ModifiedAtUtc);

public sealed record NotificationTemplateRequest(
    string NotificationType,
    string Channel,
    string SubjectTemplate,
    string BodyTemplate);

public sealed record FailedNotificationResponse(
    Guid DeliveryId,
    string NotificationType,
    string Channel,
    int Attempts,
    string? ProviderCorrelationId,
    string? Failure,
    DateTimeOffset NextAttemptAtUtc);
