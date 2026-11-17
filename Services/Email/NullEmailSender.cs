using Fakturio.Api.Interfaces;

namespace Fakturio.Api.Services.Email;

public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public string ProviderName => "None";
    public bool IsConfigured => false;

    public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "Email not configured — skipped send to {To} subject={Subject}",
            message.To,
            message.Subject);
        throw new InvalidOperationException(
            "E-mailový provider nie je nakonfigurovaný. Nastav Email:Provider (Virtual, Smtp alebo Resend).");
    }
}
