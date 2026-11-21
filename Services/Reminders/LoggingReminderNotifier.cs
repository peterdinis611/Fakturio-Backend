using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;

namespace Fakturio.Api.Services.Reminders;

public sealed class LoggingReminderNotifier(ILogger<LoggingReminderNotifier> logger) : IReminderNotifier
{
    public Task<ReminderNotifyResult> NotifyAsync(
        InvoiceReminderItemResponse reminder,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Reminder (log-only) for {Number} · {Customer} · overdue={Overdue}",
            reminder.Number,
            reminder.CustomerName,
            reminder.IsOverdue);
        return Task.FromResult(new ReminderNotifyResult(
            Delivered: true,
            InAppSent: true,
            EmailAttempted: false,
            EmailSent: false));
    }
}
