using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Fx;

/// <summary>ECB daily euro foreign exchange reference rates (XML).</summary>
public sealed class EcbFxRateService(
    IHttpClientFactory httpClientFactory,
    IOptions<FxOptions> options,
    ILogger<EcbFxRateService> logger)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, Dictionary<string, decimal> Rates, DateOnly Date)> _cache =
        new();

    public async Task<ServiceResult<FxRatesResponse>> GetEurRatesAsync(
        DateOnly? onDate,
        CancellationToken ct)
    {
        if (onDate is not null)
            return Unavailable(); // daily XML is “latest” only in this thin client

        const string key = "latest";
        if (_cache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "EUR",
                cached.Date,
                cached.Rates,
                "ecb",
                onDate));
        }

        try
        {
            var client = httpClientFactory.CreateClient("ecb");
            var fullUrl = options.Value.EcbDailyFxUrl.Trim();
            var requestPath = Uri.TryCreate(fullUrl, UriKind.Absolute, out var absolute)
                ? absolute.PathAndQuery
                : fullUrl.TrimStart('/');
            using var response = await client.GetAsync(requestPath, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ECB FX returned {StatusCode}", (int)response.StatusCode);
                return Unavailable();
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            if (!TryParse(xml, out var date, out var rates) || rates.Count == 0)
                return Unavailable();

            var ttl = TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheSeconds, 60, 86_400));
            _cache[key] = (DateTimeOffset.UtcNow.Add(ttl), rates, date);

            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "EUR",
                date,
                rates,
                "ecb",
                onDate));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ECB FX parse failed");
            return Unavailable();
        }
    }

    private static bool TryParse(string xml, out DateOnly date, out Dictionary<string, decimal> rates)
    {
        rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        date = default;

        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace gesmes = "http://www.gesmes.org/Envelope/1.1";
            XNamespace ns = "http://www.ecb.int/vocabulary/20030901/daily";

            var cube = doc.Root?
                .Element(gesmes + "Cube")?
                .Element(ns + "Cube");

            var timeAttr = cube?.Attribute("time")?.Value;
            if (!string.IsNullOrWhiteSpace(timeAttr)
                && DateOnly.TryParse(timeAttr, CultureInfo.InvariantCulture, out var d))
                date = d;
            else
                date = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var rateNode in cube?.Elements(ns + "Cube") ?? [])
            {
                var currency = rateNode.Attribute("currency")?.Value?.ToUpperInvariant();
                var rateText = rateNode.Attribute("rate")?.Value;
                if (currency is null || rateText is null)
                    continue;
                if (decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                    && rate > 0)
                    rates[currency] = rate;
            }

            return rates.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static ServiceResult<FxRatesResponse> Unavailable()
        => ServiceResult<FxRatesResponse>.Fail(
            "Exchange rates are temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.FxUnavailable);
}