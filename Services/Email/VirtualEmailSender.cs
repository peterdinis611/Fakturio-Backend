using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Email;

/// <summary>
/// Dev/demo sender — pretends delivery succeeded, logs payload, no network I/O.
/// Enables full FE/BE flows and audit <c>EmailSent</c> without SMTP/Resend.
/// </summary>
public sealed class VirtualEmailSender(
    IOptions<EmailOptions> options,
    ILogger<VirtualEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public string ProviderName => "Virtual";

    public bool IsConfigured =>
        string.Equals(_options.Provider, "Virtual", StringComparison.OrdinalIgnoreCase)
        || string.Equals(_options.Provider, "Log", StringComparison.OrdinalIgnoreCase);

    public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var attachmentNames = message.Attachments is { Count: > 0 }
            ? string.Join(", ", message.Attachments.Select(a => $"{a.FileName} ({a.Content.Length} B)"))
            : "—";

        logger.LogInformation(
            "Virtual email → {To} | from={From} | replyTo={ReplyTo} | subject={Subject} | attachments={Attachments}",
            message.To,
            message.FromAddressOverride ?? _options.FromAddress,
            message.ReplyTo ?? _options.ReplyTo ?? "—",
            message.Subject,
            attachmentNames);

        return Task.CompletedTask;
    }
}
