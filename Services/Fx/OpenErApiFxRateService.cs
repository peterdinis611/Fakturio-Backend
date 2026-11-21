using System.Globalization;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Fx;

/// <summary>
/// Final FX fill-in via open.er-api.com (free, no key) — after ECB/NBS/Frankfurter.
/// </summary>
public sealed class OpenErApiFxRateService(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenErApiFxRateService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ServiceResult<FxRatesResponse>> GetLatestAsync(
        string @base,
        IReadOnlyList<string> symbols,
        CancellationToken ct = default)
    {
        var baseCode = FxHelpers.NormalizeCode(@base) ?? "EUR";
        try
        {
            var client = httpClientFactory.CreateClient("exchangerate");
            using var response = await client.GetAsync($"latest/{baseCode}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<FxRatesResponse>.Fail(
                    $"open.er-api HTTP {(int)response.StatusCode}",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (!root.TryGetProperty("rates", out var ratesEl) || ratesEl.ValueKind != JsonValueKind.Object)
            {
                return ServiceResult<FxRatesResponse>.Fail(
                    "open.er-api missing rates",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FxUnavailable);
            }

            DateOnly? date = null;
            if (root.TryGetProperty("time_last_update_utc", out var t)
                && t.ValueKind == JsonValueKind.String
                && DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                date = DateOnly.FromDateTime(dt.ToUniversalTime());
            }

            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var sym in symbols)
            {
                if (ratesEl.TryGetProperty(sym, out var v) && v.TryGetDecimal(out var d) && d > 0)
                    rates[sym] = d;
            }

            if (rates.Count == 0)
            {
                return ServiceResult<FxRatesResponse>.Fail(
                    "open.er-api no matching symbols",
                    StatusCodes.Status404NotFound,
                    ErrorCodes.NotFound);
            }

            return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
                Base: baseCode,
                Date: date ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Rates: rates,
                Source: "open-er-api"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "open.er-api FX failed");
            return ServiceResult<FxRatesResponse>.Fail(
                "open.er-api unavailable",
                StatusCodes.Status502BadGateway,
                ErrorCodes.FxUnavailable);
        }
    }
}
