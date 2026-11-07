using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Fx;

/// <summary>
/// Prefers ČNB for CZK pairs; Frankfurter (ECB) for everything else / fill-in.
/// Supports historical rates via <see cref="GetOnDateAsync"/>.
/// </summary>
public sealed class CompositeFxRateService(
    FrankfurterFxRateService frankfurter,
    CnbFxRateService cnb,
    ILogger<CompositeFxRateService> logger) : IFxRateService
{
    public Task<ServiceResult<FxRatesResponse>> GetLatestAsync(
        string @base = "EUR",
        IReadOnlyList<string>? symbols = null,
        CancellationToken ct = default) =>
        GetAsync(null, @base, symbols, ct);

    public Task<ServiceResult<FxRatesResponse>> GetOnDateAsync(
        DateOnly date,
        string @base = "EUR",
        IReadOnlyList<string>? symbols = null,
        CancellationToken ct = default) =>
        GetAsync(date, @base, symbols, ct);

    private async Task<ServiceResult<FxRatesResponse>> GetAsync(
        DateOnly? onDate,
        string @base,
        IReadOnlyList<string>? symbols,
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

        var needsCzk = baseCode == "CZK" || symbolList.Contains("CZK", StringComparer.Ordinal);
        var otherSymbols = symbolList.Where(s => s != "CZK").ToArray();

        Dictionary<string, decimal> rates = new(StringComparer.Ordinal);
        var sources = new List<string>();
        DateOnly? effectiveDate = null;

        if (needsCzk)
        {
            var cnbResult = await cnb.GetCzkRatesAsync(onDate, ct);
            if (cnbResult.Success && cnbResult.Data is { } cnbData)
            {
                effectiveDate = cnbData.Date;
                sources.Add("cnb");

                if (baseCode == "EUR" && symbolList.Contains("CZK"))
                {
                    if (cnbData.Rates.TryGetValue("EUR", out var eurInCzk) && eurInCzk > 0)
                        rates["CZK"] = eurInCzk;
                }
                else if (baseCode == "CZK")
                {
                    foreach (var sym in symbolList)
                    {
                        if (cnbData.Rates.TryGetValue(sym, out var perUnit) && perUnit > 0)
                            rates[sym] = 1m / perUnit;
                    }
                }
                else if (cnbData.Rates.TryGetValue(baseCode, out var baseInCzk) && baseInCzk > 0
                         && symbolList.Contains("CZK"))
                {
                    rates["CZK"] = baseInCzk;
                }
            }
            else
            {
                logger.LogDebug("CNB unavailable; falling back to Frankfurter for CZK");
            }
        }

        var frankfurterSymbols = otherSymbols.ToList();
        if (symbolList.Contains("CZK", StringComparer.Ordinal) && !rates.ContainsKey("CZK"))
            frankfurterSymbols.Add("CZK");

        if (frankfurterSymbols.Count > 0 || rates.Count == 0)
        {
            var ff = onDate is { } d
                ? await frankfurter.GetOnDateAsync(d, baseCode, frankfurterSymbols.Count > 0 ? frankfurterSymbols : symbolList, ct)
                : await frankfurter.GetLatestAsync(baseCode, frankfurterSymbols.Count > 0 ? frankfurterSymbols : symbolList, ct);

            if (!ff.Success || ff.Data is null)
            {
                if (rates.Count == 0)
                    return ff;
            }
            else
            {
                effectiveDate ??= ff.Data.Date;
                sources.Add("frankfurter");
                foreach (var (k, v) in ff.Data.Rates)
                {
                    if (!rates.ContainsKey(k))
                        rates[k] = v;
                }
            }
        }

        if (rates.Count == 0)
        {
            return ServiceResult<FxRatesResponse>.Fail(
                "Exchange rates are temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.FxUnavailable);
        }

        return ServiceResult<FxRatesResponse>.Ok(new FxRatesResponse(
            Base: baseCode,
            Date: effectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Rates: rates,
            Source: string.Join('+', sources.Distinct()),
            RequestedDate: onDate));
    }
}
