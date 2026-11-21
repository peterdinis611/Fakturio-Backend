using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IVatComplyService
{
    Task<ServiceResult<EuVatRatesResponse>> GetVatRatesAsync(string? country, CancellationToken ct = default);
    Task<ServiceResult<GeolocateResponse>> GeolocateAsync(string? ip, CancellationToken ct = default);
    Task<(bool Ok, string? BankName, string? Bic, string? Country)> TryLookupIbanAsync(
        string iban,
        CancellationToken ct = default);
}
