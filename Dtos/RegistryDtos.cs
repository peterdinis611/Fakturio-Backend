namespace Fakturio.Api.Dtos;

/// <summary>Normalized company record from a public business register (ARES / ORSF / RPO / FS).</summary>
public record RegistryCompanyResponse(
    string Country,
    string Source,
    string Ico,
    string Name,
    string? LegalForm,
    string? Dic,
    string? IcDph,
    string? Street,
    string? City,
    string? PostalCode,
    string CountryCode,
    bool? IsVatPayer,
    string? CommercialRegister
);

public record RegistrySearchHitResponse(
    string Ico,
    string Name,
    string? City,
    string? IcDph,
    bool? IsVatPayer,
    string? LegalForm,
    string Source
);

public record RegistrySearchResponse(
    string Query,
    IReadOnlyList<RegistrySearchHitResponse> Hits
);

public record VatVerificationResponse(
    bool IsValid,
    string CountryCode,
    string VatNumber,
    string? Name,
    string? Address,
    string Source,
    string? UserError
);
