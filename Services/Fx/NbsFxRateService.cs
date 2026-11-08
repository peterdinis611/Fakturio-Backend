using System.Collections.Concurrent;
using System.Globalization;
using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Fx;

/// <summary>
/// NBS publishes ECB reference rates (foreign currency units per 1 EUR) as CSV.
/// </summary>
public sealed class NbsFxRateService(
    IHttpClientFactory httpClientFactory,
    IOptions<FxOptions> options,
    ILogger<NbsFxRateService> logger)
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, Dictionary<string, decimal> Rates, DateOnly Date)> _cache =
        new();

    public async Task<ServiceResult<FxRatesResponse>> GetEurRatesAsync(
        DateOnly? onDate,
        CancellationToken ct)
    {
        var key = onDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "latest";
        if (_cache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
        {
            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "EUR",
                cached.Date,
                cached.Rates,
                "nbs",
                onDate));
        }

        try
        {
            var client = httpClientFactory.CreateClient("nbs");
            var path = onDate is { } d
                ? $"{d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}/csv"
                : "latest/csv";

            using var response = await client.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("NBS returned {StatusCode}", (int)response.StatusCode);
                return Unavailable();
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            if (!TryParse(text, out var date, out var rates) || rates.Count == 0)
                return Unavailable();

            var ttl = TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheSeconds, 60, 86_400));
            _cache[key] = (DateTimeOffset.UtcNow.Add(ttl), rates, date);
            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                "EUR",
                date,
                rates,
                "nbs",
                onDate));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NBS FX lookup failed");
            return Unavailable();
        }
    }

    private static ServiceResult<FxRatesResponse> Unavailable() =>
        ServiceResult<FxRatesResponse>.Fail(
            "NBS exchange rates are temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.FxUnavailable);

    private static bool TryParse(
        string text,
        out DateOnly date,
        out Dictionary<string, decimal> rates)
    {
        date = default;
        rates = new Dictionary<string, decimal>(StringComparer.Ordinal);

        using var reader = new StringReader(text.TrimStart('\uFEFF'));
        var header = reader.ReadLine();
        var data = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(data))
            return false;

        var headers = SplitCsv(header);
        var values = SplitCsv(data);
        if (headers.Length == 0 || values.Length == 0 || headers.Length != values.Length)
            return false;

        // First column is date (Dátum / Date), rest are currency codes.
        var dateRaw = values[0].Trim();
        if (!DateOnly.TryParseExact(dateRaw, "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && !DateOnly.TryParseExact(dateRaw, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return false;
        }

        for (var i = 1; i < headers.Length; i++)
        {
            var code = headers[i].Trim().ToUpperInvariant();
            if (code.Length != 3)
                continue;

            var raw = values[i]
                .Replace("\"", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace('\u00A0', ' ')
                .Trim()
                .Replace(',', '.');

            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                || rate <= 0)
            {
                continue;
            }

            rates[code] = rate;
        }

        return rates.Count > 0;
    }

    private static string[] SplitCsv(string line) =>
        line.Split(';', StringSplitOptions.None);
}
