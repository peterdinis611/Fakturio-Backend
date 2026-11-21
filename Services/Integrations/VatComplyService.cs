using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// VATcomply — free TEDB VAT rates, IBAN hint, geolocate (≈2 req/s).
/// </summary>
public sealed class VatComplyService(
    IHttpClientFactory httpClientFactory,
    ILogger<VatComplyService> logger) : IVatComplyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly ConcurrentDictionary<string, (DateTime Expires, EuVatRatesResponse Data)> VatCache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<ServiceResult<EuVatRatesResponse>> GetVatRatesAsync(
        string? country,
        CancellationToken ct = default)
    {
        var cc = string.IsNullOrWhiteSpace(country)
            ? null
            : country.Trim().ToUpperInvariant();
        var cacheKey = cc ?? "*";
        if (VatCache.TryGetValue(cacheKey, out var hit) && hit.Expires > DateTime.UtcNow)
            return ServiceResult<EuVatRatesResponse>.Ok(hit.Data);

        try
        {
            var client = httpClientFactory.CreateClient("vatcomply");
            using var response = await client.GetAsync("vat_rates", ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("VATcomply vat_rates HTTP {Status}", (int)response.StatusCode);
                return SoftRates(cc);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var ratesEl = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rates", out var r)
                ? r
                : root;

            var countries = new List<EuVatRateCountryItem>();
            // Live API returns an array: [{ country_code, standard_rate, reduced_rates: [..] }]
            if (ratesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in ratesEl.EnumerateArray())
                {
                    var code = GetString(el, "country_code") ?? GetString(el, "country");
                    if (code is null) continue;
                    var item = ParseCountry(code, el);
                    if (item is not null) countries.Add(item);
                }
            }
            else if (ratesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in ratesEl.EnumerateObject())
                {
                    var item = ParseCountry(prop.Name, prop.Value);
                    if (item is not null) countries.Add(item);
                }
            }

            EuVatRatesResponse payload;
            if (cc is not null)
            {
                var match = countries.FirstOrDefault(c =>
                    string.Equals(c.Country, cc, StringComparison.OrdinalIgnoreCase));
                payload = match is null
                    ? new EuVatRatesResponse(cc, null, [], countries, "vatcomply-tedb-miss")
                    : new EuVatRatesResponse(match.Country, match.Standard, match.Reduced, countries, "vatcomply-tedb");
            }
            else
            {
                payload = new EuVatRatesResponse(null, null, [], countries, "vatcomply-tedb");
            }

            VatCache[cacheKey] = (DateTime.UtcNow.AddHours(24), payload);
            return ServiceResult<EuVatRatesResponse>.Ok(payload);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "VATcomply vat_rates failed");
            return SoftRates(cc);
        }
    }

    public async Task<ServiceResult<GeolocateResponse>> GeolocateAsync(
        string? ip,
        CancellationToken ct = default)
    {
        var requested = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        if (requested is not null)
        {
            if (!IPAddress.TryParse(requested, out var parsed))
            {
                return ServiceResult<GeolocateResponse>.BadRequest(
                    "ip is not a valid IP address.",
                    ErrorCodes.BadRequest);
            }

            if (parsed.IsIPv4MappedToIPv6)
                parsed = parsed.MapToIPv4();
            requested = parsed.ToString();

            if (IsNonPublic(parsed))
            {
                return ServiceResult<GeolocateResponse>.Ok(new(
                    requested, null, null, "vatcomply-geolocate-private-ip"));
            }
        }

        try
        {
            var client = httpClientFactory.CreateClient("vatcomply");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                requested is null ? "geolocate" : $"geolocate?ip={Uri.EscapeDataString(requested)}");
            if (requested is not null)
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", requested);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<GeolocateResponse>.Ok(new(
                    requested, null, null, "vatcomply-geolocate-soft-fail"));
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var resolvedIp = GetString(json, "ip");

            // VATcomply geolocates the *caller* IP and may ignore ?ip=. Never return another
            // client's location as if it belonged to the requested address.
            if (requested is not null && resolvedIp is not null
                && !string.Equals(resolvedIp, requested, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<GeolocateResponse>.Ok(new(
                    requested, null, null, "vatcomply-geolocate-ip-unsupported"));
            }

            var countryCode = GetString(json, "country_code") ?? GetString(json, "iso2");
            var countryName = GetString(json, "name") ?? GetString(json, "country_name");
            return ServiceResult<GeolocateResponse>.Ok(new(
                resolvedIp ?? requested, countryCode, countryName, "vatcomply-geolocate"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "VATcomply geolocate failed");
            return ServiceResult<GeolocateResponse>.Ok(new(
                requested, null, null, "vatcomply-geolocate-soft-fail"));
        }
    }

    private static bool IsNonPublic(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;
        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] is >= 16 and <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254)
               || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    public async Task<(bool Ok, string? BankName, string? Bic, string? Country)> TryLookupIbanAsync(
        string iban,
        CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("vatcomply");
            using var response = await client.GetAsync(
                $"iban?iban={Uri.EscapeDataString(iban)}",
                ct);
            if (!response.IsSuccessStatusCode) return (false, null, null, null);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var valid = json.TryGetProperty("valid", out var v) && v.ValueKind == JsonValueKind.True;
            if (!valid && json.TryGetProperty("iban", out _))
                valid = true;

            var bank = GetString(json, "bank") ?? GetNested(json, "bank_data", "name");
            var bic = GetString(json, "bic") ?? GetNested(json, "bank_data", "bic");
            var country = GetString(json, "country_code") ?? GetString(json, "country");
            return (valid || bank is not null, bank, bic, country);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "VATcomply IBAN lookup failed");
            return (false, null, null, null);
        }
    }

    private static ServiceResult<EuVatRatesResponse> SoftRates(string? country) =>
        ServiceResult<EuVatRatesResponse>.Ok(new(
            country, null, [], [], "vatcomply-tedb-soft-fail"));

    private static EuVatRateCountryItem? ParseCountry(string code, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        decimal? standard = null;
        var reduced = new List<decimal>();

        if (el.TryGetProperty("standard_rate", out var std) && TryDecimal(std, out var s))
            standard = s;
        else if (el.TryGetProperty("standard", out var std2) && TryDecimal(std2, out var s2))
            standard = s2;

        if (el.TryGetProperty("reduced_rates", out var redRates) && redRates.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in redRates.EnumerateArray())
            {
                if (TryDecimal(item, out var rv)) reduced.Add(rv);
            }
        }
        else if (el.TryGetProperty("reduced_rates", out var red) && red.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in red.EnumerateObject())
            {
                if (TryDecimal(p.Value, out var rv)) reduced.Add(rv);
            }
        }
        else if (el.TryGetProperty("reduced", out var redArr) && redArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in redArr.EnumerateArray())
            {
                if (TryDecimal(item, out var rv)) reduced.Add(rv);
            }
        }

        if (standard is null) return null;
        return new EuVatRateCountryItem(code.ToUpperInvariant(), standard.Value, reduced.Distinct().OrderBy(x => x).ToList());
    }

    private static bool TryDecimal(JsonElement el, out decimal value)
    {
        value = 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out value)) return true;
        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;
        return false;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var p)
        && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string? GetNested(JsonElement el, string obj, string name) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(obj, out var child)
            ? GetString(child, name)
            : null;
}
