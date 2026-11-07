using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IFxRateService
{
    Task<ServiceResult<FxRatesResponse>> GetLatestAsync(
        string @base = "EUR",
        IReadOnlyList<string>? symbols = null,
        CancellationToken ct = default);
}
