using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// Placeholder notifier — logs candidates. Replace with email/SMTP when ready.
/// </summary>
public sealed class LoggingReminderNotifier(ILogger<LoggingReminderNotifier> logger) : IReminderNotifier
{
    public Task NotifyAsync(InvoiceReminderItemResponse reminder, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Reminder candidate {Number} → {Customer} ({Email}); due {DueDate}; overdue={Overdue}; daysUntilDue={Days}; priorCount={Count}",
            reminder.Number,
            reminder.CustomerName,
            reminder.CustomerEmail ?? "—",
            reminder.DueDate,
            reminder.IsOverdue,
            reminder.DaysUntilDue,
            reminder.ReminderCount);

        return Task.CompletedTask;
    }
}
