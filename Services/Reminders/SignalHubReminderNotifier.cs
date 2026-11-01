using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// Creates in-app notifications (SignalR) for reminder candidates, respecting user preferences.
/// Email channel is preference-gated and logged until SMTP is wired.
/// </summary>
public sealed class SignalHubReminderNotifier(
    AppDbContext db,
    INotificationService notifications,
    ILogger<SignalHubReminderNotifier> logger) : IReminderNotifier
{
    public async Task NotifyAsync(InvoiceReminderItemResponse reminder, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == reminder.Id, ct);
        if (invoice is null)
            return;

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == invoice.CompanyId, ct);
        if (company is null)
            return;

        var prefs = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == company.UserId, ct);

        if (prefs is not null && !prefs.RemindersEnabled)
        {
            logger.LogDebug(
                "Reminder skipped for {Number}: user disabled reminders.",
                reminder.Number);
            return;
        }

        if (prefs is not null && prefs.OnlyOverdue && !reminder.IsOverdue)
        {
            logger.LogDebug(
                "Reminder skipped for {Number}: only-overdue preference.",
                reminder.Number);
            return;
        }

        if (prefs is not null && !prefs.OnlyOverdue && !reminder.IsOverdue
            && reminder.DaysUntilDue > prefs.WithinDays)
        {
            logger.LogDebug(
                "Reminder skipped for {Number}: outside withinDays={WithinDays}.",
                reminder.Number,
                prefs.WithinDays);
            return;
        }

        var title = reminder.IsOverdue
            ? $"Po splatnosti · {reminder.Number}"
            : reminder.DaysUntilDue == 0
                ? $"Splatné dnes · {reminder.Number}"
                : $"Splatnosť o {reminder.DaysUntilDue} d. · {reminder.Number}";

        var body =
            $"{reminder.CustomerName} · {reminder.Total:0.00} {reminder.Currency}"
            + (string.IsNullOrWhiteSpace(reminder.CustomerEmail)
                ? ""
                : $" · {reminder.CustomerEmail}");

        var published = await notifications.PublishAsync(
            company.UserId,
            company.Id,
            title,
            body,
            $"/invoices/{reminder.Id}",
            reminder.Id,
            reminder.Number,
            ct);

        if (published is not null)
        {
            logger.LogInformation(
                "In-app reminder queued for {Number} → user {UserId}",
                reminder.Number,
                company.UserId);
        }

        if (prefs?.EmailEnabled == true)
        {
            // SMTP not wired yet — keep an audit trail so prefs are meaningful.
            logger.LogInformation(
                "Email reminder preferred for {Number} → {Email} (SMTP not configured; skipped send).",
                reminder.Number,
                reminder.CustomerEmail ?? company.Email ?? "—");
        }
    }
}
