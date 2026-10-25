using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

public interface ICompanyService
{
    Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default);

    Task<CompanySettingsResponse> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default);
}
