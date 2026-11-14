using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

/// <summary>ČÚZK RÚIAN geocode — Czech addresses (no key).</summary>
public sealed partial class RuianAddressService(
    IHttpClientFactory httpClientFactory,
    ILogger<RuianAddressService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, (DateTimeOffset Exp, AddressNormalizeResponse Value)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<ServiceResult<AddressNormalizeResponse>?> TryLookupAsync(
        string query,
        CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 3)
            return null;

        var cacheKey = q;
        if (Cache.TryGetValue(cacheKey, out var cached) && cached.Exp > DateTimeOffset.UtcNow)
            return ServiceResult<AddressNormalizeResponse>.Ok(cached.Value);

        try
        {
            var client = httpClientFactory.CreateClient("ruian");
            var url =
                $"findAddressCandidates?SingleLine={Uri.EscapeDataString(q)}&outSR=4326&maxLocations=1&f=json";
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("RÚIAN returned {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<RuianPayload>(JsonOptions, ct);
            var candidate = payload?.Candidates?.FirstOrDefault();
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.Address))
                return null;

            var display = candidate.Address.Trim();
            var (street, city, postal) = ParseCzechAddress(display);
            double? lat = candidate.Location?.Y;
            double? lon = candidate.Location?.X;

            var result = new AddressNormalizeResponse(
                DisplayName: display,
                Street: street,
                City: city,
                PostalCode: postal,
                CountryCode: "CZ",
                Lat: lat,
                Lon: lon,
                Source: "ruian");

            Cache[cacheKey] = (DateTimeOffset.UtcNow.AddHours(24), result);
            return ServiceResult<AddressNormalizeResponse>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RÚIAN lookup failed for {Query}", q);
            return null;
        }
    }

    private static (string? Street, string? City, string? Postal) ParseCzechAddress(string display)
    {
        // "Karlova 187/6, Staré Město, 11000 Praha 1"
        var parts = display.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var street = parts.Length > 0 ? parts[0] : null;
        string? city = null;
        string? postal = null;

        if (parts.Length > 1)
        {
            var last = parts[^1];
            var m = PostalCityRegex().Match(last);
            if (m.Success)
            {
                postal = NormalizePostal(m.Groups[1].Value);
                city = m.Groups[2].Value.Trim();
            }
            else
            {
                city = last;
            }
        }

        return (street, city, postal);
    }

    private static string? NormalizePostal(string digits)
    {
        var d = new string(digits.Where(char.IsDigit).ToArray());
        return d.Length == 5 ? $"{d[..3]} {d[3..]}" : digits.Trim();
    }

    [GeneratedRegex(@"^(\d{5})\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCityRegex();

    private sealed class RuianPayload
    {
        public List<RuianCandidate>? Candidates { get; set; }
    }

    private sealed class RuianCandidate
    {
        public string? Address { get; set; }
        public RuianLocation? Location { get; set; }
    }

    private sealed class RuianLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}