using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class ServiceCatalogMapper
{
    public static ServiceCatalogItemResponse ToResponse(ServiceCatalogItem s) => new(
        s.Id,
        s.Description,
        s.Unit,
        s.UnitPrice,
        s.VatRate,
        s.IsActive,
        s.SortOrder,
        s.CreatedAt,
        s.UpdatedAt);

    public static ServiceCatalogItem FromCreate(
        CreateServiceCatalogItemRequest request,
        decimal defaultVat) => new()
    {
        Description = request.Description.Trim(),
        Unit = string.IsNullOrWhiteSpace(request.Unit) ? "ks" : request.Unit.Trim(),
        UnitPrice = request.UnitPrice,
        VatRate = request.VatRate ?? defaultVat,
        IsActive = request.IsActive ?? true,
        SortOrder = request.SortOrder ?? 0,
    };

    public static void ApplyUpdate(ServiceCatalogItem item, UpdateServiceCatalogItemRequest request)
    {
        if (request.Description is not null)
            item.Description = request.Description.Trim();
        if (request.Unit is not null)
            item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "ks" : request.Unit.Trim();
        if (request.UnitPrice.HasValue)
            item.UnitPrice = request.UnitPrice.Value;
        if (request.VatRate.HasValue)
            item.VatRate = request.VatRate.Value;
        if (request.IsActive.HasValue)
            item.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue)
            item.SortOrder = request.SortOrder.Value;
        item.UpdatedAt = DateTime.UtcNow;
    }
}
