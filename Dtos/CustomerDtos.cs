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
