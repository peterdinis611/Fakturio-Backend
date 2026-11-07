using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Fx;

/// <summary>ČNB daily FX fixing — rates are CZK per 1 (or N) units of foreign currency.</summary>
public sealed class CnbFxRateService(
    IHttpClientFactory httpClientFactory,
    IOptions<FxOptions> options,
    ILogger<CnbFxRateService> logger)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, Dictionary<string, decimal> Rates, DateOnly Date)> _cache = new();

    public async Task<ServiceResult<FxRatesResponse>> GetCzkRatesAsync(
        DateOnly? onDate,
        CancellationToken ct)
    {
        var key = onDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest";
        if (_cache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "CZK",
                cached.Date,
                cached.Rates,
                "cnb",
                onDate));
        }

        try
        {
            var client = httpClientFactory.CreateClient("cnb");
            var path = onDate is { } d
                ? $"denni_kurz.txt?date={d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}"
                : "denni_kurz.txt";

            using var response = await client.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CNB returned {StatusCode}", (int)response.StatusCode);
                return ServiceResult<FxRatesResponse>.Fail(
                    "CNB exchange rates are temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            if (!TryParse(text, out var date, out var rates) || rates.Count == 0)
            {
                return ServiceResult<FxRatesResponse>.Fail(
                    "CNB exchange rates are temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            var ttl = TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheSeconds, 60, 86_400));
            _cache[key] = (DateTimeOffset.UtcNow.Add(ttl), rates, date);
            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "CZK",
                date,
                rates,
                "cnb",
                onDate));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CNB FX lookup failed");
            return ServiceResult<FxRatesResponse>.Fail(
                "CNB exchange rates are temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.FxUnavailable);
        }
    }

    private static bool TryParse(
        string text,
        out DateOnly date,
        out Dictionary<string, decimal> rates)
    {
        date = default;
        rates = new Dictionary<string, decimal>(StringComparer.Ordinal);

        using var reader = new StringReader(text);
        var header = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(header))
            return false;

        // "06.11.2026 #214"
        var datePart = header.Split('#', 2)[0].Trim();
        if (!DateOnly.TryParseExact(datePart, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return false;

        _ = reader.ReadLine(); // column headers

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = line.Split('|');
            if (cols.Length < 5)
                continue;

            var amountRaw = cols[2].Trim().Replace(',', '.');
            var code = cols[3].Trim().ToUpperInvariant();
            var rateRaw = cols[4].Trim().Replace(',', '.');

            if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0
                || !decimal.TryParse(rateRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                || rate <= 0
                || code.Length != 3)
            {
                continue;
            }

            // Store as CZK per 1 unit of foreign currency.
            rates[code] = rate / amount;
        }

        return rates.Count > 0;
    }
}
