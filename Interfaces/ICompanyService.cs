using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICompanyService
{
    Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default);

    Task<CompanySummaryResponse> GetSummaryAsync(CancellationToken ct = default);

    Task<CompanyCompletenessResponse> GetStatusAsync(CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateDefaultsAsync(
        UpdateCompanyDefaultsRequest request,
        CancellationToken ct = default);
}
