using System.Net;
using System.Net.Mail;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public string ProviderName => "Smtp";

    public bool IsConfigured =>
        string.Equals(_options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.FromAddress)
        && !string.IsNullOrWhiteSpace(_options.Smtp.Host);

    public async Task SendAsync(OutboundEmail message, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("SMTP e-mail nie je nakonfigurovaný.");

        var smtp = _options.Smtp;
        using var mail = new MailMessage
        {
            From = new MailAddress(
                message.FromAddressOverride ?? _options.FromAddress,
                message.FromNameOverride ?? _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);

        var replyTo = message.ReplyTo ?? _options.ReplyTo;
        if (!string.IsNullOrWhiteSpace(replyTo))
            mail.ReplyToList.Add(replyTo);

        var streams = new List<MemoryStream>();
        try
        {
            if (message.Attachments is { Count: > 0 })
            {
                foreach (var attachment in message.Attachments)
                {
                    var stream = new MemoryStream(attachment.Content);
                    streams.Add(stream);
                    mail.Attachments.Add(new Attachment(
                        stream,
                        attachment.FileName,
                        attachment.ContentType));
                }
            }

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                client.Credentials = new NetworkCredential(
                    smtp.Username,
                    smtp.Password ?? string.Empty);
            }

            ct.ThrowIfCancellationRequested();
            await client.SendMailAsync(mail, ct);
            logger.LogInformation(
                "SMTP email sent to {To} subject={Subject}",
                message.To,
                message.Subject);
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }
}
