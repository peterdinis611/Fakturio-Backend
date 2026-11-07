using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Fx;

/// <summary>ECB rates via Frankfurter (latest + historical).</summary>
public sealed class FrankfurterFxRateService(
    IHttpClientFactory httpClientFactory,
    IOptions<FxOptions> options,
    ILogger<FrankfurterFxRateService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, FxRatesResponse Value)> _cache = new();

    public Task<ServiceResult<FxRatesResponse>> GetLatestAsync(
        string @base,
        IReadOnlyList<string> symbols,
        CancellationToken ct) =>
        FetchAsync("latest", @base, symbols, requestedDate: null, ct);

    public Task<ServiceResult<FxRatesResponse>> GetOnDateAsync(
        DateOnly date,
        string @base,
        IReadOnlyList<string> symbols,
        CancellationToken ct) =>
        FetchAsync(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), @base, symbols, date, ct);

    private async Task<ServiceResult<FxRatesResponse>> FetchAsync(
        string pathOrDate,
        string @base,
        IReadOnlyList<string> symbols,
        DateOnly? requestedDate,
        CancellationToken ct)
    {
        var baseCode = FxHelpers.NormalizeCode(@base) ?? "EUR";
        var symbolList = FxHelpers.NormalizeSymbols(symbols, baseCode);
        if (symbolList.Length == 0)
        {
            return ServiceResult<FxRatesResponse>.BadRequest(
                "Provide at least one target currency symbol.",
                ErrorCodes.BadRequest);
        }

        var cacheKey = $"ff:{pathOrDate}:{baseCode}:{string.Join(',', symbolList)}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
            return ServiceResult<FxRatesResponse>.Ok(cached.Value);

        try
        {
            var client = httpClientFactory.CreateClient("frankfurter");
            var qs =
                $"{pathOrDate}?base={Uri.EscapeDataString(baseCode)}&symbols={Uri.EscapeDataString(string.Join(',', symbolList))}";
            using var response = await client.GetAsync(qs, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Frankfurter returned {StatusCode} for {Query}", (int)response.StatusCode, qs);
                return ServiceResult<FxRatesResponse>.Fail(
                    "Exchange rates are temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            var payload = await response.Content.ReadFromJsonAsync<FrankfurterPayload>(JsonOptions, ct);
            if (payload?.Rates is null || payload.Rates.Count == 0)
            {
                return ServiceResult<FxRatesResponse>.Fail(
                    "Exchange rates are temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            var date = DateOnly.TryParse(payload.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : DateOnly.FromDateTime(DateTime.UtcNow);

            var result = new FxRatesResponse(
                Base: payload.Base ?? baseCode,
                Date: date,
                Rates: payload.Rates
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                    .ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value, StringComparer.Ordinal),
                Source: "frankfurter",
                RequestedDate: requestedDate);

            var ttl = TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheSeconds, 60, 86_400));
            _cache[cacheKey] = (DateTimeOffset.UtcNow.Add(ttl), result);
            return ServiceResult<FxRatesResponse>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Frankfurter FX lookup failed");
            return ServiceResult<FxRatesResponse>.Fail(
                "Exchange rates are temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.FxUnavailable);
        }
    }

    private sealed class FrankfurterPayload
    {
        public string? Base { get; set; }
        public string? Date { get; set; }
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
