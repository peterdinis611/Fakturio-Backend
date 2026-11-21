using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

public sealed record ReminderNotifyResult(
    bool Delivered,
    bool InAppSent,
    bool EmailAttempted,
    bool EmailSent);

public interface IReminderNotifier
{
    /// <summary>
    /// Deliver reminder via in-app and/or email.
    /// <see cref="ReminderNotifyResult.Delivered"/> is true only when at least one
    /// configured channel succeeded (and email succeeded if email was required).
    /// </summary>
    Task<ReminderNotifyResult> NotifyAsync(
        InvoiceReminderItemResponse reminder,
        CancellationToken ct = default);
}
