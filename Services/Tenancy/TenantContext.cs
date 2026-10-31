using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Tenancy;

public sealed class TenantContext(
    ICurrentActorAccessor actors,
    AppDbContext db) : ITenantContext
{
    private Guid? _userId;
    private Guid? _companyId;

    public async Task<Guid> GetUserIdAsync(CancellationToken ct = default)
    {
        if (_userId is { } cached)
            return cached;

        var actor = await actors.GetAsync(ct);
        if (actor.UserId is null || actor.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("Authentication required.");

        _userId = actor.UserId.Value;
        return _userId.Value;
    }

    public async Task<Guid> GetCompanyIdAsync(CancellationToken ct = default)
    {
        if (_companyId is { } cached)
            return cached;

        var userId = await GetUserIdAsync(ct);
        var company = await db.CompanySettings
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (company is null)
        {
            company = new CompanySettings
            {
                UserId = userId,
                Name = string.Empty,
                DefaultDueDays = 14,
                DefaultVatRate = 23m,
                DefaultCurrency = "EUR",
                DefaultPaymentMethod = "bank_transfer",
                Country = "SK",
                IsVatPayer = true,
            };
            db.CompanySettings.Add(company);
            await db.SaveChangesAsync(ct);
        }

        _companyId = company.Id;
        return _companyId.Value;
    }

    public async Task<(Guid UserId, Guid CompanyId)> GetAsync(CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync(ct);
        var companyId = await GetCompanyIdAsync(ct);
        return (userId, companyId);
    }
}

public static class TenantQueryExtensions
{
    public static IQueryable<T> ForCompany<T>(this IQueryable<T> query, Guid companyId)
        where T : class, ICompanyOwned =>
        query.Where(e => e.CompanyId == companyId);
}
