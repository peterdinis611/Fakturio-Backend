using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

/// <summary>
/// Dispatches a payment reminder for one invoice (email, SMS, …).
/// Default implementation logs only until a real channel is wired.
/// </summary>
public interface IReminderNotifier
{
    Task NotifyAsync(InvoiceReminderItemResponse reminder, CancellationToken ct = default);
}
