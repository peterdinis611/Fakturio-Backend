using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IFxRateService
{
    Task<ServiceResult<FxRatesResponse>> GetLatestAsync(
        string @base = "EUR",
        IReadOnlyList<string>? symbols = null,
        CancellationToken ct = default);

    /// <summary>Rates for a calendar day (weekends/holidays → previous business day).</summary>
    Task<ServiceResult<FxRatesResponse>> GetOnDateAsync(
        DateOnly date,
        string @base = "EUR",
        IReadOnlyList<string>? symbols = null,
        CancellationToken ct = default);
}
