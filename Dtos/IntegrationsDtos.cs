namespace Fakturio.Api.Dtos;

public record IbanLookupResponse(
    bool IsValid,
    string Iban,
    string? CountryCode,
    string? BankCode,
    string? BankName,
    string? Bic,
    string? City,
    string Source
);

public record FsVatPayerResponse(
    bool IsVatPayer,
    string Ico,
    string? Dic,
    string? IcDph,
    string? Name,
    string Source
);

public record AddressNormalizeResponse(
    string DisplayName,
    string? Street,
    string? City,
    string? PostalCode,
    string? CountryCode,
    double? Lat,
    double? Lon,
    string Source
);

public record OrsrCompanySummaryResponse(
    string Ico,
    string Name,
    string? FileReference,
    string? CourtName,
    string? Street,
    string? CityPostal,
    int DocumentsCount,
    IReadOnlyList<OrsrDocumentItem> Documents,
    string Source
);

public record OrsrDocumentItem(
    string? Title,
    string? SubmittedAt,
    string? Type
);

public record AdisregAccountItem(
    string Account,
    string? BankCode,
    string? PublishedAt,
    bool IsStandard
);

public record AdisregStatusResponse(
    string Dic,
    string Status,
    bool IsUnreliable,
    bool Found,
    string? TaxOfficeCode,
    string? UnreliablePublishedAt,
    IReadOnlyList<AdisregAccountItem> Accounts,
    string Source
);

public record IsirProceedingItem(
    string? Name,
    string? CourtFile,
    string? State,
    string? City,
    string? DetailUrl
);

public record IsirStatusResponse(
    string Ico,
    bool HasProceedings,
    int Count,
    IReadOnlyList<IsirProceedingItem> Proceedings,
    string Source
);

public record RegisterUzUnitResponse(
    string Ico,
    string? Dic,
    string Name,
    string? Street,
    string? City,
    string? PostalCode,
    string? FoundedAt,
    string? CancelledAt,
    int StatementsCount,
    int AnnualReportsCount,
    string Source
);

public record PostalValidateResponse(
    bool IsValid,
    string Code,
    string Country,
    bool Exists,
    string? City,
    string? DisplayName,
    string? NormalizedCode,
    string Source
);
