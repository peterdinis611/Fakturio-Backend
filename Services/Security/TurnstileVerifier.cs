using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Security;

public sealed class TurnstileVerifier(
    IHttpClientFactory httpClientFactory,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerifier> logger) : ITurnstileVerifier
{
    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(cfg.Secret))
        {
            logger.LogWarning("Turnstile enabled but Secret is missing");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var client = httpClientFactory.CreateClient("turnstile");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = cfg.Secret,
                ["response"] = token.Trim(),
                ["remoteip"] = remoteIp ?? string.Empty
            });

            using var response = await client.PostAsync("siteverify", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile verify HTTP {Status}", (int)response.StatusCode);
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<TurnstileVerifyPayload>(cancellationToken: ct);
            return payload?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Turnstile verification failed");
            return false;
        }
    }

    private sealed class TurnstileVerifyPayload
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}