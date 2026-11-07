using System.Collections.Concurrent;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Tenancy;

public sealed class TenantContext(
    ICurrentActorAccessor actors,
    AppDbContext db) : ITenantContext
{
    /// <summary>
    /// Dashboard (and similar) fire parallel requests; each scoped TenantContext
    /// would otherwise race-create CompanySettings for the same UserId.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CompanyCreateGates = new();

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
        var existing = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (existing is not null)
        {
            _companyId = existing.Id;
            return _companyId.Value;
        }

        var gate = CompanyCreateGates.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            existing = await db.CompanySettings.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (existing is not null)
            {
                _companyId = existing.Id;
                return _companyId.Value;
            }

            var company = new CompanySettings
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
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Parallel request won the insert — drop the failed entry and load theirs.
                db.Entry(company).State = EntityState.Detached;
                existing = await db.CompanySettings.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                    ?? throw new InvalidOperationException(
                        $"CompanySettings missing after unique conflict for user {userId}.");

                _companyId = existing.Id;
                return _companyId.Value;
            }

            _companyId = company.Id;
            return _companyId.Value;
        }
        finally
        {
            gate.Release();
        }
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
