using Fakturio.Api.Caching;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Company;

public class CompanyService(
    AppDbContext db,
    ICacheInvalidator cache,
    ILogoStorageService logos,
    ITenantContext tenant) : ICompanyService
{
    public async Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        return CompanyMapper.ToResponse(company);
    }

    public async Task<CompanySummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        return CompanyMapper.ToSummary(company);
    }

    public async Task<CompanyCompletenessResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        return CompanyMapper.BuildCompleteness(company);
    }

    public async Task<CompanyBankResponse> GetBankAsync(CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        return CompanyMapper.ToBank(company);
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CompanySettingsResponse>.BadRequest(
                "Company name is required.",
                ErrorCodes.BadRequest);

        if (request.IsVatPayer == true && string.IsNullOrWhiteSpace(request.IcDph))
            return ServiceResult<CompanySettingsResponse>.BadRequest(
                "IČ DPH is required when the company is a VAT payer.",
                ErrorCodes.BadRequest);

        var company = await GetOrCreateAsync(ct);
        CompanyMapper.Apply(company, request);
        await SaveAndInvalidateAsync(ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UpdateDefaultsAsync(
        UpdateCompanyDefaultsRequest request,
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        CompanyMapper.ApplyDefaults(company, request);
        await SaveAndInvalidateAsync(ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UpdateBankAsync(
        UpdateCompanyBankRequest request,
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        CompanyMapper.ApplyBank(company, request);
        await SaveAndInvalidateAsync(ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UpdateBrandingAsync(
        UpdateCompanyBrandingRequest request,
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        CompanyMapper.ApplyBranding(company, request);
        await SaveAndInvalidateAsync(ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UpdateContactAsync(
        UpdateCompanyContactRequest request,
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        CompanyMapper.ApplyContact(company, request);
        await SaveAndInvalidateAsync(ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> UploadLogoAsync(
        IFormFile file,
        CancellationToken ct = default)
    {
        var saved = await logos.SaveAsync(file, ct);
        if (!saved.Success || saved.Data is null)
            return ServiceResult<CompanySettingsResponse>.Fail(
                saved.Error ?? "Upload failed.",
                saved.StatusCode,
                saved.ErrorCode);

        var company = await GetOrCreateAsync(ct);
        var previous = company.LogoUrl;
        company.LogoUrl = saved.Data;
        company.UpdatedAt = DateTime.UtcNow;
        await SaveAndInvalidateAsync(ct);

        if (!string.Equals(previous, saved.Data, StringComparison.OrdinalIgnoreCase))
            await logos.DeleteIfLocalAsync(previous, ct);

        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    public async Task<ServiceResult<CompanySettingsResponse>> DeleteLogoAsync(
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        var previous = company.LogoUrl;
        company.LogoUrl = null;
        company.UpdatedAt = DateTime.UtcNow;
        await SaveAndInvalidateAsync(ct);
        await logos.DeleteIfLocalAsync(previous, ct);
        return ServiceResult<CompanySettingsResponse>.Ok(CompanyMapper.ToResponse(company));
    }

    private async Task<CompanySettings> GetOrCreateAsync(CancellationToken ct)
    {
        var userId = await tenant.GetUserIdAsync(ct);
        var company = await db.CompanySettings.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (company is not null)
            return company;

        company = new CompanySettings
        {
            UserId = userId,
            Name = string.Empty,
            DefaultDueDays = 14,
            DefaultVatRate = 23m,
            DefaultCurrency = "EUR",
            DefaultPaymentMethod = "bank_transfer",
            Country = "SK",
            IsVatPayer = true
        };
        db.CompanySettings.Add(company);
        await db.SaveChangesAsync(ct);
        return company;
    }

    private async Task SaveAndInvalidateAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await cache.InvalidateCompanyAsync(ct);
        await cache.InvalidateReportsAsync(ct);
    }
}
