using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Registry;

public sealed partial class VatVerificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<VatVerificationService> logger) : IVatVerificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<VatVerificationResponse>> VerifyAsync(
        string vatId,
        CancellationToken ct = default)
    {
        if (!TryParseVatId(vatId, out var country, out var number))
        {
            return ServiceResult<VatVerificationResponse>.BadRequest(
                "Enter a valid EU VAT ID (e.g. SK2121585004).",
                ErrorCodes.InvalidVatId);
        }

        try
        {
            var client = httpClientFactory.CreateClient("vies");
            using var response = await client.GetAsync(
                $"ms/{country}/vat/{Uri.EscapeDataString(number)}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "VIES returned {StatusCode} for {Country}/{Vat}",
                    (int)response.StatusCode,
                    country,
                    number);
                return ServiceResult<VatVerificationResponse>.Fail(
                    "VIES is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.ViesUnavailable);
            }

            var payload = await response.Content.ReadFromJsonAsync<ViesPayload>(JsonOptions, ct);
            if (payload is null)
            {
                return ServiceResult<VatVerificationResponse>.Fail(
                    "VIES is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.ViesUnavailable);
            }

            return ServiceResult<VatVerificationResponse>.Ok(new VatVerificationResponse(
                IsValid: payload.IsValid,
                CountryCode: country,
                VatNumber: number,
                Name: Normalize(payload.Name),
                Address: Normalize(payload.Address),
                Source: "vies",
                UserError: Normalize(payload.UserError)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "VIES verify failed for {VatId}", vatId);
            return ServiceResult<VatVerificationResponse>.Fail(
                "VIES is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.ViesUnavailable);
        }
    }

    internal static bool TryParseVatId(string? raw, out string country, out string number)
    {
        country = string.Empty;
        number = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var cleaned = VatCleanupRegex().Replace(raw.Trim().ToUpperInvariant(), "");
        var match = VatIdRegex().Match(cleaned);
        if (!match.Success)
            return false;

        country = match.Groups[1].Value;
        number = match.Groups[2].Value;
        return country.Length == 2 && number.Length is >= 2 and <= 12;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) || value is "---"
            ? null
            : value.Trim();

    [GeneratedRegex(@"[^A-Z0-9]")]
    private static partial Regex VatCleanupRegex();

    [GeneratedRegex(@"^([A-Z]{2})([A-Z0-9]{2,12})$")]
    private static partial Regex VatIdRegex();

    private sealed class ViesPayload
    {
        public bool IsValid { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? UserError { get; set; }
        public string? VatNumber { get; set; }
        public string? OriginalVatNumber { get; set; }
    }
}
