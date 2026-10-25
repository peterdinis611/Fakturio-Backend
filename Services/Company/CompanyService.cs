using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Company;

public class CompanyService(AppDbContext db) : ICompanyService
{
    public async Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        return CompanyMapper.ToResponse(company);
    }

    public async Task<CompanySettingsResponse> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default)
    {
        var company = await GetOrCreateAsync(ct);
        CompanyMapper.Apply(company, request);
        await db.SaveChangesAsync(ct);
        return CompanyMapper.ToResponse(company);
    }

    private async Task<CompanySettings> GetOrCreateAsync(CancellationToken ct)
    {
        var company = await db.CompanySettings.FirstOrDefaultAsync(ct);
        if (company is not null)
            return company;

        company = new CompanySettings
        {
            Name = string.Empty,
            DefaultDueDays = 14,
            DefaultVatRate = 23m,
            DefaultCurrency = "EUR",
            Country = "SK"
        };
        db.CompanySettings.Add(company);
        await db.SaveChangesAsync(ct);
        return company;
    }
}
