namespace Fakturio.Api.Dtos;

/// <summary>Normalized company record from a public business register (ARES / RPO / FS).</summary>
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
