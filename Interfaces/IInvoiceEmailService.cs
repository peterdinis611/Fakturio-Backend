using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceEmailService
{
    EmailStatusResponse GetStatus();

    Task<ServiceResult<SendTestEmailResponse>> SendTestAsync(
        SendTestEmailRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<SendInvoiceEmailResponse>> SendInvoiceAsync(
        Guid invoiceId,
        SendInvoiceEmailRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a payment reminder email for a reminder candidate (no tenant context required).
    /// Returns false when skipped (no recipient / prefs / provider).
    /// </summary>
    Task<bool> TrySendReminderAsync(
        InvoiceReminderItemResponse reminder,
        Guid companyId,
        Guid userId,
        CancellationToken ct = default);
}
