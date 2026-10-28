using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.ServiceCatalog;

public sealed class ServiceCatalogService(AppDbContext db, ICompanyService company) : IServiceCatalogService
{
    public async Task<PagedResponse<ServiceCatalogItemResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = ServiceGuards.NormalizeOptionalSearch(search);

        var query = db.ServiceCatalogItems.AsNoTracking().AsQueryable();
        if (activeOnly == true)
            query = query.Where(s => s.IsActive);
        if (term is not null)
            query = query.Where(s => s.Description.Contains(term));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Description)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ServiceCatalogItemResponse>(
            items.Select(ServiceCatalogMapper.ToResponse).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceCatalogItemResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var item = await db.ServiceCatalogItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return item is null ? null : ServiceCatalogMapper.ToResponse(item);
    }

    public async Task<ServiceResult<ServiceCatalogItemResponse>> CreateAsync(
        CreateServiceCatalogItemRequest request,
        CancellationToken ct = default)
    {
        var settings = await company.GetAsync(ct);
        var isVatPayer = settings.IsVatPayer;
        var defaultVat = isVatPayer ? settings.DefaultVatRate : 0m;

        var entity = ServiceCatalogMapper.FromCreate(request, defaultVat);
        if (!isVatPayer)
            entity.VatRate = 0m;

        db.ServiceCatalogItems.Add(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ServiceCatalogItemResponse>.Created(
            ServiceCatalogMapper.ToResponse(entity));
    }

    public async Task<ServiceResult<ServiceCatalogItemResponse>> UpdateAsync(
        Guid id,
        UpdateServiceCatalogItemRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.ServiceCatalogItems.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult<ServiceCatalogItemResponse>.NotFound(
                "Service not found.",
                ErrorCodes.NotFound);

        ServiceCatalogMapper.ApplyUpdate(entity, request);

        var settings = await company.GetAsync(ct);
        if (!settings.IsVatPayer)
            entity.VatRate = 0m;

        await db.SaveChangesAsync(ct);
        return ServiceResult<ServiceCatalogItemResponse>.Ok(
            ServiceCatalogMapper.ToResponse(entity));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.ServiceCatalogItems.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult.NotFound("Service not found.", ErrorCodes.NotFound);

        db.ServiceCatalogItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }
}
