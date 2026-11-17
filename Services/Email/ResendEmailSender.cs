using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Email;

public sealed class ResendEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public string ProviderName => "Resend";

    public bool IsConfigured =>
        string.Equals(_options.Provider, "Resend", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.FromAddress)
        && !string.IsNullOrWhiteSpace(_options.Resend.ApiKey);

    public async Task SendAsync(OutboundEmail message, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Resend e-mail nie je nakonfigurovaný.");

        var fromAddress = message.FromAddressOverride ?? _options.FromAddress;
        var fromName = message.FromNameOverride ?? _options.FromName;
        var from = string.IsNullOrWhiteSpace(fromName)
            ? fromAddress
            : $"{fromName} <{fromAddress}>";

        var payload = new ResendSendRequest(
            from,
            [message.To],
            message.Subject,
            message.HtmlBody,
            message.TextBody,
            message.ReplyTo ?? _options.ReplyTo,
            message.Attachments?
                .Select(a => new ResendAttachment(
                    a.FileName,
                    Convert.ToBase64String(a.Content)))
                .ToList());

        var client = httpClientFactory.CreateClient("resend");
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.Resend.ApiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "Resend send failed ({Status}): {Body}",
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException(
                $"Odoslanie cez Resend zlyhalo ({(int)response.StatusCode}).");
        }

        logger.LogInformation(
            "Resend email sent to {To} subject={Subject}",
            message.To,
            message.Subject);
    }

    private sealed record ResendSendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("reply_to")] string? ReplyTo,
        [property: JsonPropertyName("attachments")] List<ResendAttachment>? Attachments);

    private sealed record ResendAttachment(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content")] string Content);
}
