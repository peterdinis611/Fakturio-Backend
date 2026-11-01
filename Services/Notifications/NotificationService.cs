using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Hubs;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Notifications;

public sealed class NotificationService(
    AppDbContext db,
    ITenantContext tenant,
    IHubContext<SignalHub> hub) : INotificationService
{
    public async Task<NotificationListResponse> ListAsync(int take = 30, CancellationToken ct = default)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        take = Math.Clamp(take, 1, 100);

        var items = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var unread = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return new NotificationListResponse(items.Select(ToResponse).ToList(), unread);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        return await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task<ServiceResult> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        var entity = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (entity is null)
            return ServiceResult.NotFound("Notification not found.", ErrorCodes.NotFound);

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await hub.Clients.Group(SignalHub.UserGroup(userId))
                .SendAsync("unreadCount", await UnreadCountFor(userId, ct), ct);
        }

        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<ServiceResult> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await db.SaveChangesAsync(ct);
            await hub.Clients.Group(SignalHub.UserGroup(userId))
                .SendAsync("unreadCount", 0, ct);
        }

        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<NotificationPreferencesResponse> GetPreferencesAsync(CancellationToken ct = default)
    {
        var prefs = await GetOrCreatePreferencesAsync(ct);
        return ToPrefsResponse(prefs);
    }

    public async Task<NotificationPreferencesResponse> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct = default)
    {
        var prefs = await GetOrCreatePreferencesAsync(ct);

        if (request.RemindersEnabled is { } reminders)
            prefs.RemindersEnabled = reminders;
        if (request.InAppEnabled is { } inApp)
            prefs.InAppEnabled = inApp;
        if (request.EmailEnabled is { } email)
            prefs.EmailEnabled = email;
        if (request.WithinDays is { } within)
            prefs.WithinDays = Math.Clamp(within, 0, 90);
        if (request.OnlyOverdue is { } onlyOverdue)
            prefs.OnlyOverdue = onlyOverdue;

        if (request.ClearQuietHours)
        {
            prefs.QuietHoursStartUtc = null;
            prefs.QuietHoursEndUtc = null;
        }
        else
        {
            if (request.QuietHoursStartUtc is { } start)
                prefs.QuietHoursStartUtc = Math.Clamp(start, 0, 23);
            if (request.QuietHoursEndUtc is { } end)
                prefs.QuietHoursEndUtc = Math.Clamp(end, 0, 23);
        }

        prefs.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToPrefsResponse(prefs);
    }

    public async Task<NotificationResponse?> PublishAsync(
        Guid userId,
        Guid companyId,
        string title,
        string body,
        string? href,
        Guid? invoiceId,
        string? invoiceNumber,
        CancellationToken ct = default)
    {
        var prefs = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (prefs is not null && (!prefs.RemindersEnabled || !prefs.InAppEnabled))
            return null;

        if (prefs is not null && IsInQuietHours(prefs, DateTime.UtcNow))
            return null;

        var entity = new AppNotification
        {
            UserId = userId,
            CompanyId = companyId,
            Kind = NotificationKind.Reminder,
            Title = title,
            Body = body,
            Href = href,
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
        };

        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct);

        var response = ToResponse(entity);
        await PushAsync(userId, response, ct);
        return response;
    }

    public async Task<NotificationResponse> PublishTestAsync(CancellationToken ct = default)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        var companyId = await tenant.GetCompanyIdAsync(ct);

        var entity = new AppNotification
        {
            UserId = userId,
            CompanyId = companyId,
            Kind = NotificationKind.System,
            Title = "Skúšobná notifikácia",
            Body = "Signal Hub je pripojený — toto je test do zvončeka.",
            Href = "/settings/notifications",
        };

        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct);

        var response = ToResponse(entity);
        await PushAsync(userId, response, ct);
        return response;
    }

    private async Task PushAsync(Guid userId, NotificationResponse response, CancellationToken ct)
    {
        await hub.Clients.Group(SignalHub.UserGroup(userId))
            .SendAsync("notification", response, ct);
        await hub.Clients.Group(SignalHub.UserGroup(userId))
            .SendAsync("unreadCount", await UnreadCountFor(userId, ct), ct);
    }

    private async Task<NotificationPreferences> GetOrCreatePreferencesAsync(CancellationToken ct)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        var prefs = await db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (prefs is not null)
            return prefs;

        prefs = new NotificationPreferences { UserId = userId };
        db.NotificationPreferences.Add(prefs);
        await db.SaveChangesAsync(ct);
        return prefs;
    }

    private async Task<int> UnreadCountFor(Guid userId, CancellationToken ct) =>
        await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    internal static bool IsInQuietHours(NotificationPreferences prefs, DateTime utcNow)
    {
        if (prefs.QuietHoursStartUtc is null || prefs.QuietHoursEndUtc is null)
            return false;

        var start = prefs.QuietHoursStartUtc.Value;
        var end = prefs.QuietHoursEndUtc.Value;
        var hour = utcNow.Hour;

        if (start == end)
            return false;

        // Window that does not wrap midnight, e.g. 22→6 wraps.
        if (start < end)
            return hour >= start && hour < end;

        return hour >= start || hour < end;
    }

    private static NotificationResponse ToResponse(AppNotification n) => new(
        n.Id,
        n.Kind,
        n.Title,
        n.Body,
        n.Href,
        n.InvoiceId,
        n.InvoiceNumber,
        n.IsRead,
        n.CreatedAt,
        n.ReadAt);

    private static NotificationPreferencesResponse ToPrefsResponse(NotificationPreferences p) => new(
        p.RemindersEnabled,
        p.InAppEnabled,
        p.EmailEnabled,
        p.WithinDays,
        p.OnlyOverdue,
        p.QuietHoursStartUtc,
        p.QuietHoursEndUtc,
        p.UpdatedAt);
}
