namespace Fakturio.Api.Dtos;

public record CreateCustomerRequest(
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    string? Email,
    string? Phone,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country
);

public record UpdateCustomerRequest(
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    string? Email,
    string? Phone,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country
);

public record CustomerResponse(
    Guid Id,
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    string? Email,
    string? Phone,
    string? Street,
    string? City,
    string? PostalCode,
    string Country,
    DateTime CreatedAt,
    int InvoiceCount
);

public record CustomerLookupItem(
    Guid Id,
    string Name,
    string? Ico,
    string? City
);

public record CustomerSummaryResponse(
    Guid Id,
    string Name,
    string? Ico,
    int InvoiceCount,
    int DraftCount,
    int IssuedCount,
    int PaidCount,
    int OverdueCount,
    int CancelledCount,
    decimal TotalInvoiced,
    decimal PaidAmount,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    string Currency,
    DateOnly? LastInvoiceDate
);
