namespace Fakturio.Api.Dtos;

public record UpdateCompanySettingsRequest(
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    string? Email,
    string? Phone,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country,
    string? BankIban,
    string? BankBic,
    string? BankName,
    int? DefaultDueDays,
    decimal? DefaultVatRate,
    string? DefaultCurrency,
    string? InvoicePrefix,
    string? Notes
);

public record CompanySettingsResponse(
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
    string? BankIban,
    string? BankBic,
    string? BankName,
    int DefaultDueDays,
    decimal DefaultVatRate,
    string DefaultCurrency,
    string? InvoicePrefix,
    string? Notes,
    DateTime UpdatedAt
);
