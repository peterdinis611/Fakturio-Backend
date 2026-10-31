using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record RecurringInvoiceItemRequest(
    Guid? ServiceCatalogItemId,
    string Description,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate
);

public record CreateRecurringInvoiceRequest(
    Guid CustomerId,
    string Name,
    RecurrenceInterval Interval,
    int? DayOfMonth,
    int? DueDays,
    DateOnly? NextRunOn,
    bool? IsActive,
    string? Notes,
    IReadOnlyList<RecurringInvoiceItemRequest> Items
);

public record UpdateRecurringInvoiceRequest(
    Guid CustomerId,
    string Name,
    RecurrenceInterval Interval,
    int? DayOfMonth,
    int? DueDays,
    DateOnly? NextRunOn,
    bool? IsActive,
    string? Notes,
    IReadOnlyList<RecurringInvoiceItemRequest> Items
);

public record RecurringInvoiceItemResponse(
    Guid Id,
    Guid? ServiceCatalogItemId,
    string Description,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    int SortOrder
);

public record RecurringInvoiceListItemResponse(
    Guid Id,
    string Name,
    Guid CustomerId,
    string CustomerName,
    RecurrenceInterval Interval,
    int DayOfMonth,
    int DueDays,
    bool IsActive,
    DateOnly NextRunOn,
    DateOnly? LastGeneratedOn,
    Guid? LastInvoiceId,
    int ItemCount,
    DateTime UpdatedAt
);

public record RecurringInvoiceResponse(
    Guid Id,
    string Name,
    Guid CustomerId,
    string CustomerName,
    RecurrenceInterval Interval,
    int DayOfMonth,
    int DueDays,
    bool IsActive,
    DateOnly NextRunOn,
    DateOnly? LastGeneratedOn,
    Guid? LastInvoiceId,
    string? Notes,
    IReadOnlyList<RecurringInvoiceItemResponse> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record RecurringRunResult(int GeneratedCount, int SkippedCount, IReadOnlyList<Guid> InvoiceIds);
