using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICompanyService
{
    Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default);

    Task<CompanySummaryResponse> GetSummaryAsync(CancellationToken ct = default);

    Task<CompanyCompletenessResponse> GetStatusAsync(CancellationToken ct = default);

    Task<CompanyBankResponse> GetBankAsync(CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateDefaultsAsync(
        UpdateCompanyDefaultsRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateBankAsync(
        UpdateCompanyBankRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateBrandingAsync(
        UpdateCompanyBrandingRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UpdateContactAsync(
        UpdateCompanyContactRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> UploadLogoAsync(
        IFormFile file,
        CancellationToken ct = default);

    Task<ServiceResult<CompanySettingsResponse>> DeleteLogoAsync(CancellationToken ct = default);
}
