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
    string Source,
    string? WarningCode = null,
    string? WarningMessage = null
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

public record AresVrPersonItem(
    string Name,
    string? Role,
    string? Organ,
    string? Since
);

public record AresVrSummaryResponse(
    string Ico,
    string? Name,
    string? CourtFile,
    string? RegisteredCapital,
    IReadOnlyList<AresVrPersonItem> Officers,
    int OfficersCount,
    string Source
);

public record RegisterUzStatementItem(
    long Id,
    string? Type,
    string? PeriodFrom,
    string? PeriodTo,
    string? AssembledAt,
    string? FiledAt,
    int ReportsCount
);

public record RegisterUzStatementsResponse(
    string Ico,
    string? Name,
    IReadOnlyList<RegisterUzStatementItem> Statements,
    string Source
);

public record BusinessDayResponse(
    string Country,
    DateOnly From,
    int AddDays,
    DateOnly Result,
    bool Adjusted,
    IReadOnlyList<string> HolidaysSkipped,
    string Source
);

public record RpvsOwnerItem(
    string Name,
    bool IsPublicOfficial,
    string? ValidFrom,
    string? ValidTo
);

public record RpvsRegistrationItem(
    long Id,
    string? Name,
    string? ValidFrom,
    string? ValidTo,
    bool IsActive,
    long? PartnerId,
    int? FileNumber
);

public record RpvsStatusResponse(
    string Ico,
    bool IsRegistered,
    string? Name,
    long? PartnerId,
    int? FileNumber,
    string? PortalUrl,
    IReadOnlyList<RpvsRegistrationItem> Registrations,
    IReadOnlyList<RpvsOwnerItem> BeneficialOwners,
    string Source
);

public record RegistrSmluvContractItem(
    string? SubjectName,
    string? Description,
    string? PublishedAt,
    string? Value,
    string? PartyName,
    string? DetailUrl
);

public record RegistrSmluvStatusResponse(
    string Ico,
    bool Found,
    int TotalCount,
    string PortalUrl,
    IReadOnlyList<RegistrSmluvContractItem> Recent,
    string Source
);

public record LeiRecordItem(
    string Lei,
    string? LegalName,
    string? Jurisdiction,
    string? RegisteredAs,
    string? EntityStatus,
    string? RegistrationStatus,
    string? NextRenewalDate,
    string? City,
    string? Country,
    string? DetailUrl
);

public record LeiLookupResponse(
    string Query,
    bool Found,
    int Count,
    IReadOnlyList<LeiRecordItem> Records,
    string Source
);

public record SanctionsHitItem(
    string? Id,
    string Name,
    string? Schema,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Topics,
    string? DetailUrl
);

public record SanctionsSearchResponse(
    string Query,
    int Count,
    IReadOnlyList<SanctionsHitItem> Hits,
    string Source
);

public record SkObchodnyVestnikResponse(
    string Ico,
    bool Found,
    string? Name,
    string PortalUrl,
    string Source
);

public record CountryItem(
    string Code,
    string? Code3,
    string Name,
    string? CallingCode
);

public record CountriesListResponse(
    IReadOnlyList<CountryItem> Countries,
    string Source
);

public record EmailTypoHintResponse(
    string Email,
    bool HasSuggestion,
    string? SuggestedEmail,
    string? Note,
    string Source
);

public record PeppolParticipantItem(
    string? ParticipantId,
    string? Name,
    string? Country,
    string? Scheme,
    IReadOnlyList<string> DocumentTypes
);

public record PeppolLookupResponse(
    string Ico,
    bool Found,
    IReadOnlyList<PeppolParticipantItem> Participants,
    string Source
);

public record BankCodeLookupResponse(
    string Code,
    string Country,
    string? Name,
    string? Bic,
    bool Found,
    string Source
);

public record BankCodeListItem(string Code, string Name, string? Bic);

public record BankCodeListResponse(
    string Country,
    IReadOnlyList<BankCodeListItem> Items,
    string Source
);

public record EuVatRatesResponse(
    string? Country,
    decimal? Standard,
    IReadOnlyList<decimal> Reduced,
    IReadOnlyList<EuVatRateCountryItem> Countries,
    string Source
);

public record EuVatRateCountryItem(
    string Country,
    decimal Standard,
    IReadOnlyList<decimal> Reduced
);

public record GeolocateResponse(
    string? Ip,
    string? CountryCode,
    string? CountryName,
    string Source
);

public record MfEinvoiceItem(
    string? Id,
    string? Format,
    string? CustomerName,
    string? SupplierName,
    string? CustomerIco,
    string? SupplierIco,
    string? IssuedAt
);

public record MfEinvoiceLookupResponse(
    string Ico,
    bool Found,
    IReadOnlyList<MfEinvoiceItem> Invoices,
    string Source
);

public record CzJusticeLinkItem(string Title, string Url);

public record CzJusticePdfResponse(
    string Ico,
    bool Found,
    IReadOnlyList<CzJusticeLinkItem> Links,
    string Source
);

public record OpenDataDatasetItem(
    string? Title,
    string? Url,
    string? Organization,
    string? Notes
);

public record OpenDataSearchResponse(
    string Query,
    string Country,
    bool Found,
    IReadOnlyList<OpenDataDatasetItem> Datasets,
    string Source
);

public record AvatarLookupResponse(
    string EmailHash,
    string GravatarUrl,
    string LibravatarUrl,
    string Source
);

public record PasswordCheckRequest(string Password);

public record PasswordCheckResponse(
    bool Pwned,
    int Count,
    string Source
);
