using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

/// <summary>OSM Nominatim — strict rate limit; in-memory cache.</summary>
public sealed class NominatimAddressService(
    IHttpClientFactory httpClientFactory,
    ILogger<NominatimAddressService> logger) : IAddressNormalizeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, (DateTimeOffset Exp, AddressNormalizeResponse Value)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCall = DateTimeOffset.MinValue;

    public async Task<ServiceResult<AddressNormalizeResponse>> NormalizeAsync(
        string query,
        string? countryCode = "SK",
        CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 3)
        {
            return ServiceResult<AddressNormalizeResponse>.BadRequest(
                "Address query must be at least 3 characters.",
                ErrorCodes.BadRequest);
        }

        var cc = string.IsNullOrWhiteSpace(countryCode)
            ? "sk"
            : countryCode.Trim().ToLowerInvariant();
        var cacheKey = $"{cc}|{q}";

        if (Cache.TryGetValue(cacheKey, out var cached) && cached.Exp > DateTimeOffset.UtcNow)
            return ServiceResult<AddressNormalizeResponse>.Ok(cached.Value);

        await Gate.WaitAsync(ct);
        try
        {
            var since = DateTimeOffset.UtcNow - _lastCall;
            if (since < TimeSpan.FromSeconds(1.1))
                await Task.Delay(TimeSpan.FromSeconds(1.1) - since, ct);

            var client = httpClientFactory.CreateClient("nominatim");
            var url =
                $"search?q={Uri.EscapeDataString(q)}&format=json&addressdetails=1&limit=1&countrycodes={Uri.EscapeDataString(cc)}";
            using var response = await client.GetAsync(url, ct);
            _lastCall = DateTimeOffset.UtcNow;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim returned {StatusCode}", (int)response.StatusCode);
                return ServiceResult<AddressNormalizeResponse>.Fail(
                    "Address lookup is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.AddressUnavailable);
            }

            var hits = await response.Content.ReadFromJsonAsync<List<NominatimHit>>(JsonOptions, ct);
            var hit = hits?.FirstOrDefault();
            if (hit is null || string.IsNullOrWhiteSpace(hit.DisplayName))
            {
                return ServiceResult<AddressNormalizeResponse>.NotFound(
                    "No address match found.",
                    ErrorCodes.NotFound);
            }

            var addr = hit.Address;
            var street = BuildStreet(addr);
            var city = FirstNonEmpty(addr?.City, addr?.Town, addr?.Village, addr?.Municipality, addr?.Suburb);
            var postal = NormalizePostal(addr?.Postcode);
            var country = addr?.CountryCode?.ToUpperInvariant() ?? cc.ToUpperInvariant();

            double? lat = double.TryParse(hit.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
                ? la
                : null;
            double? lon = double.TryParse(hit.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
                ? lo
                : null;

            var result = new AddressNormalizeResponse(
                DisplayName: hit.DisplayName.Trim(),
                Street: street,
                City: city,
                PostalCode: postal,
                CountryCode: country,
                Lat: lat,
                Lon: lon,
                Source: "nominatim");

            Cache[cacheKey] = (DateTimeOffset.UtcNow.AddHours(24), result);
            return ServiceResult<AddressNormalizeResponse>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nominatim failed for {Query}", q);
            return ServiceResult<AddressNormalizeResponse>.Fail(
                "Address lookup is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.AddressUnavailable);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string? BuildStreet(NominatimAddress? a)
    {
        if (a is null)
            return null;
        var road = FirstNonEmpty(a.Road, a.Pedestrian, a.Path);
        var num = FirstNonEmpty(a.HouseNumber);
        if (road is null)
            return num;
        return num is null ? road : $"{road} {num}";
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

    private sealed class NominatimHit
    {
        public string? DisplayName { get; set; }
        public string? Lat { get; set; }
        public string? Lon { get; set; }
        public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        public string? Road { get; set; }
        public string? Pedestrian { get; set; }
        public string? Path { get; set; }
        [JsonPropertyName("house_number")]
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? Town { get; set; }
        public string? Village { get; set; }
        public string? Municipality { get; set; }
        public string? Suburb { get; set; }
        public string? Postcode { get; set; }
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
    }
}
