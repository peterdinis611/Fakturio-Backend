namespace Fakturio.Api.Dtos;

public record UpdateCompanySettingsRequest(
    string Name,
    string? LegalForm,
    string? Ico,
    string? Dic,
    string? IcDph,
    bool? IsVatPayer,
    string? CommercialRegister,
    string? Email,
    string? InvoiceEmail,
    string? Phone,
    string? Website,
    string? ContactPerson,
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
    string? DefaultPaymentMethod,
    string? DefaultConstantSymbol,
    string? DefaultInvoiceNotes,
    string? InvoiceFooter,
    string? LogoUrl,
    string? Notes
);

public record UpdateCompanyDefaultsRequest(
    int? DefaultDueDays,
    decimal? DefaultVatRate,
    string? DefaultCurrency,
    string? InvoicePrefix,
    string? DefaultPaymentMethod,
    string? DefaultConstantSymbol,
    string? DefaultInvoiceNotes,
    string? InvoiceFooter
);

public record UpdateCompanyBankRequest(
    string? BankIban,
    string? BankBic,
    string? BankName,
    string? DefaultPaymentMethod,
    string? DefaultConstantSymbol
);

public record UpdateCompanyBrandingRequest(
    string? LogoUrl,
    string? Website,
    string? Notes,
    string? InvoiceFooter
);

public record UpdateCompanyContactRequest(
    string? Email,
    string? InvoiceEmail,
    string? Phone,
    string? ContactPerson,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country
);

public record CompanyBankResponse(
    string? BankIban,
    string? BankBic,
    string? BankName,
    string? DefaultPaymentMethod,
    string? DefaultConstantSymbol
);

public record CompanySettingsResponse(
    Guid Id,
    string Name,
    string? LegalForm,
    string? Ico,
    string? Dic,
    string? IcDph,
    bool IsVatPayer,
    string? CommercialRegister,
    string? Email,
    string? InvoiceEmail,
    string? Phone,
    string? Website,
    string? ContactPerson,
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
    string? DefaultPaymentMethod,
    string? DefaultConstantSymbol,
    string? DefaultInvoiceNotes,
    string? InvoiceFooter,
    string? LogoUrl,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CompanyCompletenessResponse Completeness
);

public record CompanySummaryResponse(
    Guid Id,
    string Name,
    string? LegalForm,
    string? Ico,
    string? Email,
    string? City,
    string Country,
    bool IsVatPayer,
    string? LogoUrl,
    bool IsReadyToInvoice
);

public record CompanyCompletenessResponse(
    bool IsReadyToInvoice,
    int CompletedRequired,
    int TotalRequired,
    int PercentComplete,
    IReadOnlyList<CompanyCompletenessItem> Items
);

public record CompanyCompletenessItem(
    string Key,
    string Label,
    bool IsRequired,
    bool IsComplete,
    string? Hint
);
