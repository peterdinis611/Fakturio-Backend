namespace Fakturio.Api.Dtos;

public record ServiceCatalogItemResponse(
    Guid Id,
    string Description,
    string Unit,
    decimal UnitPrice,
    decimal VatRate,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateServiceCatalogItemRequest(
    string Description,
    string? Unit,
    decimal UnitPrice,
    decimal? VatRate,
    bool? IsActive,
    int? SortOrder
);

public record UpdateServiceCatalogItemRequest(
    string? Description,
    string? Unit,
    decimal? UnitPrice,
    decimal? VatRate,
    bool? IsActive,
    int? SortOrder
);
