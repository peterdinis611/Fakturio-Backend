using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

/// <summary>
/// SK/CZ PSČ validation via format check + Nominatim postalcode search (rate-limited).
/// </summary>
public sealed partial class PostalValidateService(
    IHttpClientFactory httpClientFactory,
    ILogger<PostalValidateService> logger) : IPostalValidateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, (DateTimeOffset Exp, PostalValidateResponse Value)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCall = DateTimeOffset.MinValue;

    public async Task<ServiceResult<PostalValidateResponse>> ValidateAsync(
        string code,
        string? country = "SK",
        string? city = null,
        CancellationToken ct = default)
    {
        var cc = string.IsNullOrWhiteSpace(country)
            ? "SK"
            : country.Trim().ToUpperInvariant();

        if (cc is not ("SK" or "CZ"))
        {
            return ServiceResult<PostalValidateResponse>.BadRequest(
                "Postal validation supports SK and CZ only.",
                ErrorCodes.UnsupportedCountry);
        }

        var digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!SkCzPostalRegex().IsMatch(digits))
        {
            return ServiceResult<PostalValidateResponse>.BadRequest(
                "Postal code must be 5 digits (SK/CZ).",
                ErrorCodes.InvalidPostalCode);
        }

        var normalized = $"{digits[..3]} {digits[3..]}";
        var cityHint = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        var cacheKey = $"{cc}|{digits}|{cityHint?.ToLowerInvariant()}";

        if (Cache.TryGetValue(cacheKey, out var cached) && cached.Exp > DateTimeOffset.UtcNow)
            return ServiceResult<PostalValidateResponse>.Ok(cached.Value);

        await Gate.WaitAsync(ct);
        try
        {
            var since = DateTimeOffset.UtcNow - _lastCall;
            if (since < TimeSpan.FromSeconds(1.1))
                await Task.Delay(TimeSpan.FromSeconds(1.1) - since, ct);

            var client = httpClientFactory.CreateClient("nominatim");
            var url =
                $"search?postalcode={Uri.EscapeDataString(digits)}&countrycodes={Uri.EscapeDataString(cc.ToLowerInvariant())}&format=json&addressdetails=1&limit=5";
            using var response = await client.GetAsync(url, ct);
            _lastCall = DateTimeOffset.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim postal returned {StatusCode}", (int)response.StatusCode);
                return ServiceResult<PostalValidateResponse>.Fail(
                    "Postal lookup is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.PostalUnavailable);
            }

            var hits = await response.Content.ReadFromJsonAsync<List<NominatimHit>>(JsonOptions, ct)
                       ?? [];
            var hit = SelectHit(hits, cityHint);

            PostalValidateResponse result;
            if (hit is null)
            {
                result = new PostalValidateResponse(
                    IsValid: false,
                    Code: digits,
                    Country: cc,
                    Exists: false,
                    City: null,
                    DisplayName: null,
                    NormalizedCode: normalized,
                    Source: "nominatim");
            }
            else
            {
                var addr = hit.Address;
                var resolvedCity = FirstNonEmpty(
                    addr?.City, addr?.Town, addr?.Village, addr?.Municipality, addr?.Borough);
                var cityMatches = cityHint is null
                    || (resolvedCity is not null
                        && resolvedCity.Contains(cityHint, StringComparison.OrdinalIgnoreCase))
                    || (hit.DisplayName?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true);

                result = new PostalValidateResponse(
                    IsValid: cityMatches,
                    Code: digits,
                    Country: cc,
                    Exists: true,
                    City: resolvedCity,
                    DisplayName: hit.DisplayName?.Trim(),
                    NormalizedCode: NormalizePostal(addr?.Postcode) ?? normalized,
                    Source: "nominatim");
            }

            Cache[cacheKey] = (DateTimeOffset.UtcNow.AddHours(24), result);
            return ServiceResult<PostalValidateResponse>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Postal validation failed for {Code} {Country}", digits, cc);
            return ServiceResult<PostalValidateResponse>.Fail(
                "Postal lookup is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.PostalUnavailable);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static NominatimHit? SelectHit(List<NominatimHit> hits, string? cityHint)
    {
        if (hits.Count == 0)
            return null;
        if (cityHint is null)
            return hits[0];

        return hits.FirstOrDefault(h =>
                   (h.DisplayName?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true)
                   || (h.Address?.City?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true)
                   || (h.Address?.Town?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true)
                   || (h.Address?.Municipality?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true)
                   || (h.Address?.Borough?.Contains(cityHint, StringComparison.OrdinalIgnoreCase) == true))
               ?? hits[0];
    }

    private static string? NormalizePostal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 5)
            return $"{digits[..3]} {digits[3..]}";
        return raw.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => string.IsNullOrWhiteSpace(v) ? null : v.Trim()).FirstOrDefault(v => v is not null);

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex SkCzPostalRegex();

    private sealed class NominatimHit
    {
        public string? DisplayName { get; set; }
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        public string? City { get; set; }
        public string? Town { get; set; }
        public string? Village { get; set; }
        public string? Municipality { get; set; }
        public string? Borough { get; set; }
        public string? Postcode { get; set; }
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}
