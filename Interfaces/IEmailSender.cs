namespace Fakturio.Api.Interfaces;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record OutboundEmail(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    string? ReplyTo = null,
    string? FromAddressOverride = null,
    string? FromNameOverride = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task SendAsync(OutboundEmail message, CancellationToken ct = default);
}
