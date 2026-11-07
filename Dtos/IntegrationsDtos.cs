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
