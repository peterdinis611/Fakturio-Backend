using System.Security.Cryptography;
using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// Have I Been Pwned Passwords — k-anonymity range API (never send full hash).
/// </summary>
public sealed class HibpPasswordService(
    IHttpClientFactory httpClientFactory,
    ILogger<HibpPasswordService> logger) : IHibpPasswordService
{
    public async Task<ServiceResult<PasswordCheckResponse>> CheckAsync(
        string password,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 128)
        {
            return ServiceResult<PasswordCheckResponse>.BadRequest(
                "Password is required (max 128 chars).",
                ErrorCodes.BadRequest);
        }

        var sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)))
            .ToUpperInvariant();
        var prefix = sha1[..5];
        var suffix = sha1[5..];

        try
        {
            var client = httpClientFactory.CreateClient("hibp");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            request.Headers.TryAddWithoutValidation("Add-Padding", "true");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("HIBP HTTP {Status}", (int)response.StatusCode);
                return ServiceResult<PasswordCheckResponse>.Ok(new(
                    false, 0, "hibp-soft-fail"));
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var count = 0;
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(':');
                if (parts.Length < 2) continue;
                if (!parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                _ = int.TryParse(parts[1], out count);
                break;
            }

            return ServiceResult<PasswordCheckResponse>.Ok(new(
                count > 0, count, "hibp-range"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "HIBP check failed");
            return ServiceResult<PasswordCheckResponse>.Ok(new(
                false, 0, "hibp-soft-fail"));
        }
    }
}
