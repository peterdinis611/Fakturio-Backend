using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

public sealed class CompanyRegistryService(
    IHttpClientFactory httpClientFactory,
    ILogger<CompanyRegistryService> logger) : ICompanyRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ServiceResult<RegistryCompanyResponse>> LookupAsync(
        string country,
        string ico,
        CancellationToken ct = default)
    {
        var normalizedCountry = (country ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedIco = (ico ?? string.Empty).Trim();

        if (normalizedCountry is not ("SK" or "CZ"))
        {
            return ServiceResult<RegistryCompanyResponse>.BadRequest(
                "Supported countries are SK and CZ.",
                ErrorCodes.UnsupportedCountry);
        }

        if (string.IsNullOrWhiteSpace(normalizedIco) || !SlovakValidators.BeValidIco(normalizedIco))
        {
            return ServiceResult<RegistryCompanyResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            return normalizedCountry switch
            {
                "CZ" => await LookupAresAsync(normalizedIco, ct),
                "SK" => await LookupRpoAsync(normalizedIco, ct),
                _ => ServiceResult<RegistryCompanyResponse>.BadRequest(
                    "Supported countries are SK and CZ.",
                    ErrorCodes.UnsupportedCountry)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Company registry lookup failed for {Country}/{Ico}", normalizedCountry, normalizedIco);
            return ServiceResult<RegistryCompanyResponse>.Fail(
                "Company registry is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.RegistryUnavailable);
        }
    }

    private async Task<ServiceResult<RegistryCompanyResponse>> LookupAresAsync(
        string ico,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ares");
        using var response = await client.GetAsync($"ekonomicke-subjekty/{ico}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ServiceResult<RegistryCompanyResponse>.NotFound(
                $"No Czech company found for IČO {ico}.",
                ErrorCodes.RegistryNotFound);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "ARES returned {StatusCode} for IČO {Ico}",
                (int)response.StatusCode,
                ico);
            return ServiceResult<RegistryCompanyResponse>.Fail(
                "ARES registry is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.RegistryUnavailable);
        }

        var payload = await response.Content.ReadFromJsonAsync<AresSubject>(JsonOptions, ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ObchodniJmeno))
        {
            return ServiceResult<RegistryCompanyResponse>.NotFound(
                $"No Czech company found for IČO {ico}.",
                ErrorCodes.RegistryNotFound);
        }

        var sidlo = payload.Sidlo;
        var street = FormatCzStreet(sidlo);
        var city = FirstNonEmpty(sidlo?.NazevObce, sidlo?.NazevMestskeCastiObvodu);
        var postal = FormatPostalCode(sidlo?.Psc?.ToString(CultureInfo.InvariantCulture));
        var isVatPayer = payload.SeznamRegistraci?.StavZdrojeDph is "AKTIVNI";
        var commercialRegister = payload.DalsiUdaje?
            .Select(u => u.SpisovaZnacka)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        return ServiceResult<RegistryCompanyResponse>.Ok(new RegistryCompanyResponse(
            Country: "CZ",
            Source: "ares",
            Ico: payload.Ico ?? ico,
            Name: payload.ObchodniJmeno.Trim(),
            LegalForm: null,
            Dic: NormalizeOptional(payload.Dic),
            IcDph: null,
            Street: street,
            City: city,
            PostalCode: postal,
            CountryCode: "CZ",
            IsVatPayer: isVatPayer,
            CommercialRegister: NormalizeOptional(commercialRegister)));
    }

    private async Task<ServiceResult<RegistryCompanyResponse>> LookupRpoAsync(
        string ico,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("rpo");
        using var searchResponse = await client.GetAsync($"search?identifier={Uri.EscapeDataString(ico)}", ct);

        if (!searchResponse.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "RPO search returned {StatusCode} for IČO {Ico}",
                (int)searchResponse.StatusCode,
                ico);
            return ServiceResult<RegistryCompanyResponse>.Fail(
                "RPO registry is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.RegistryUnavailable);
        }

        var search = await searchResponse.Content.ReadFromJsonAsync<RpoSearchResponse>(JsonOptions, ct);
        var match = search?.Results?.FirstOrDefault(r =>
            r.Identifiers?.Any(id =>
                string.Equals(id.Value, ico, StringComparison.Ordinal)
                && IsCurrentlyValid(id.ValidFrom, id.ValidTo)) == true)
            ?? search?.Results?.FirstOrDefault();

        if (match is null)
        {
            return ServiceResult<RegistryCompanyResponse>.NotFound(
                $"No Slovak company found for IČO {ico}.",
                ErrorCodes.RegistryNotFound);
        }

        RpoEntity? detail = null;
        using var detailResponse = await client.GetAsync($"entity/{match.Id}", ct);
        if (detailResponse.IsSuccessStatusCode)
            detail = await detailResponse.Content.ReadFromJsonAsync<RpoEntity>(JsonOptions, ct);

        var names = detail?.FullNames ?? match.FullNames;
        var addresses = detail?.Addresses ?? match.Addresses;
        var name = CurrentValue(names)?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<RegistryCompanyResponse>.NotFound(
                $"No Slovak company found for IČO {ico}.",
                ErrorCodes.RegistryNotFound);
        }

        var address = CurrentAddress(addresses);
        var street = FormatSkStreet(address);
        var city = NormalizeOptional(address?.Municipality?.Value);
        var postal = FormatPostalCode(address?.PostalCodes?.FirstOrDefault());
        var legalForm = CurrentLegalForm(detail?.LegalForms) ?? CurrentLegalForm(match.LegalForms);
        var registrationNumber = CurrentRegistrationNumber(detail?.SourceRegister)
            ?? CurrentRegistrationNumber(match.SourceRegister);

        var company = new RegistryCompanyResponse(
            Country: "SK",
            Source: "rpo",
            Ico: ico,
            Name: name.Trim(),
            LegalForm: legalForm,
            Dic: null,
            IcDph: null,
            Street: street,
            City: city,
            PostalCode: postal,
            CountryCode: "SK",
            IsVatPayer: null,
            CommercialRegister: registrationNumber);

        return ServiceResult<RegistryCompanyResponse>.Ok(
            await EnrichSlovakTaxIdsAsync(company, ct));
    }

    /// <summary>
    /// RPO has no tax IDs — enrich DIČ / IČ DPH / VAT status from Subjekt (FS-backed).
    /// Failures are non-fatal; RPO identity/address still return.
    /// </summary>
    private async Task<RegistryCompanyResponse> EnrichSlovakTaxIdsAsync(
        RegistryCompanyResponse company,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("subjekt");
            using var response = await client.GetAsync(
                $"entity/{Uri.EscapeDataString(company.Ico)}?country=sk",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug(
                    "Subjekt tax enrichment returned {StatusCode} for IČO {Ico}",
                    (int)response.StatusCode,
                    company.Ico);
                return company;
            }

            var tax = await response.Content.ReadFromJsonAsync<SubjektEntity>(JsonOptions, ct);
            if (tax is null)
                return company;

            var dic = NormalizeOptional(tax.Dic);
            if (dic is not null && !SlovakValidators.BeValidDic(dic))
                dic = null;

            var icDph = NormalizeOptional(tax.IcDph)?.ToUpperInvariant();
            if (icDph is not null && !SlovakValidators.BeValidIcDph(icDph))
                icDph = null;

            bool? isVatPayer = icDph is not null ? true : tax.IcDph is null && dic is not null
                ? false
                : null;

            var commercialRegister = company.CommercialRegister
                ?? FormatSubjektRegistration(tax.Registration);

            return company with
            {
                Dic = dic ?? company.Dic,
                IcDph = icDph ?? company.IcDph,
                IsVatPayer = isVatPayer ?? company.IsVatPayer,
                CommercialRegister = commercialRegister,
                Source = dic is not null || icDph is not null ? "rpo+fs" : company.Source
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Subjekt tax enrichment failed for IČO {Ico}", company.Ico);
            return company;
        }
    }

    private static string? FormatSubjektRegistration(SubjektRegistration? registration)
    {
        if (registration is null)
            return null;

        var office = NormalizeOptional(registration.Office);
        var number = NormalizeOptional(registration.Number);
        if (office is null && number is null)
            return null;
        if (office is null)
            return number;
        if (number is null)
            return office;
        return $"{office}, {number}";
    }

    private static string? FormatCzStreet(AresAddress? sidlo)
    {
        if (sidlo is null)
            return null;

        if (!string.IsNullOrWhiteSpace(sidlo.NazevUlice))
        {
            var number = sidlo.CisloDomovni?.ToString(CultureInfo.InvariantCulture);
            if (sidlo.CisloOrientacni is int orientacni)
            {
                var letter = sidlo.CisloOrientacniPismeno ?? string.Empty;
                number = string.IsNullOrWhiteSpace(number)
                    ? $"{orientacni}{letter}"
                    : $"{number}/{orientacni}{letter}";
            }

            return string.IsNullOrWhiteSpace(number)
                ? sidlo.NazevUlice.Trim()
                : $"{sidlo.NazevUlice.Trim()} {number}";
        }

        return NormalizeOptional(sidlo.TextovaAdresa?.Split(',', 2)[0]);
    }

    private static string? FormatSkStreet(RpoAddress? address)
    {
        if (address is null)
            return null;

        var street = NormalizeOptional(address.Street);
        var building = NormalizeOptional(address.BuildingNumber);
        if (street is null)
            return building;

        return building is null ? street : $"{street} {building}";
    }

    private static string? FormatPostalCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 5)
            return $"{digits[..3]} {digits[3..]}";

        return raw.Trim();
    }

    private static RpoTimedValue? CurrentValue(IReadOnlyList<RpoTimedValue>? values) =>
        values?
            .Where(v => IsCurrentlyValid(v.ValidFrom, v.ValidTo))
            .OrderByDescending(v => v.ValidFrom)
            .FirstOrDefault()
        ?? values?.OrderByDescending(v => v.ValidFrom).FirstOrDefault();

    private static RpoAddress? CurrentAddress(IReadOnlyList<RpoAddress>? addresses) =>
        addresses?
            .Where(a => IsCurrentlyValid(a.ValidFrom, a.ValidTo))
            .OrderByDescending(a => a.ValidFrom)
            .FirstOrDefault()
        ?? addresses?.OrderByDescending(a => a.ValidFrom).FirstOrDefault();

    private static string? CurrentLegalForm(IReadOnlyList<RpoLegalFormEntry>? forms) =>
        forms?
            .Where(f => IsCurrentlyValid(f.ValidFrom, f.ValidTo))
            .OrderByDescending(f => f.ValidFrom)
            .Select(f => NormalizeOptional(f.Value?.Value))
            .FirstOrDefault(v => v is not null)
        ?? forms?
            .OrderByDescending(f => f.ValidFrom)
            .Select(f => NormalizeOptional(f.Value?.Value))
            .FirstOrDefault(v => v is not null);

    private static string? CurrentRegistrationNumber(RpoSourceRegister? source) =>
        source?.RegistrationNumbers?
            .Where(n => IsCurrentlyValid(n.ValidFrom, n.ValidTo))
            .OrderByDescending(n => n.ValidFrom)
            .Select(n => NormalizeOptional(n.Value))
            .FirstOrDefault(v => v is not null)
        ?? source?.RegistrationNumbers?
            .OrderByDescending(n => n.ValidFrom)
            .Select(n => NormalizeOptional(n.Value))
            .FirstOrDefault(v => v is not null);

    private static bool IsCurrentlyValid(DateOnly? validFrom, DateOnly? validTo)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (validFrom is DateOnly from && from > today)
            return false;
        if (validTo is DateOnly to && to < today)
            return false;
        return true;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(NormalizeOptional).FirstOrDefault(v => v is not null);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class AresSubject
    {
        public string? Ico { get; set; }
        public string? ObchodniJmeno { get; set; }
        public string? Dic { get; set; }
        public AresAddress? Sidlo { get; set; }
        public AresRegistrations? SeznamRegistraci { get; set; }
        public List<AresExtraData>? DalsiUdaje { get; set; }
    }

    private sealed class AresAddress
    {
        public string? NazevUlice { get; set; }
        public int? CisloDomovni { get; set; }
        public int? CisloOrientacni { get; set; }
        public string? CisloOrientacniPismeno { get; set; }
        public int? Psc { get; set; }
        public string? NazevObce { get; set; }
        public string? NazevMestskeCastiObvodu { get; set; }
        public string? TextovaAdresa { get; set; }
    }

    private sealed class AresRegistrations
    {
        public string? StavZdrojeDph { get; set; }
    }

    private sealed class AresExtraData
    {
        public string? SpisovaZnacka { get; set; }
        public string? DatovyZdroj { get; set; }
    }

    private sealed class RpoSearchResponse
    {
        public List<RpoEntity>? Results { get; set; }
    }

    private sealed class RpoEntity
    {
        public long Id { get; set; }
        public List<RpoTimedValue>? Identifiers { get; set; }
        public List<RpoTimedValue>? FullNames { get; set; }
        public List<RpoAddress>? Addresses { get; set; }
        public List<RpoLegalFormEntry>? LegalForms { get; set; }
        public RpoSourceRegister? SourceRegister { get; set; }
    }

    private sealed class RpoTimedValue
    {
        public string? Value { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
    }

    private sealed class RpoAddress
    {
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public string? Street { get; set; }
        public string? BuildingNumber { get; set; }
        public List<string>? PostalCodes { get; set; }
        public RpoCodedValue? Municipality { get; set; }
    }

    private sealed class RpoLegalFormEntry
    {
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public RpoNestedCodedValue? Value { get; set; }
    }

    private sealed class RpoNestedCodedValue
    {
        public string? Value { get; set; }
        public string? Code { get; set; }
    }

    private sealed class RpoCodedValue
    {
        public string? Value { get; set; }
        public string? Code { get; set; }
    }

    private sealed class RpoSourceRegister
    {
        public List<RpoTimedValue>? RegistrationNumbers { get; set; }
    }

    private sealed class SubjektEntity
    {
        public string? Dic { get; set; }
        [JsonPropertyName("ic_dph")]
        public string? IcDph { get; set; }
        public SubjektRegistration? Registration { get; set; }
    }

    private sealed class SubjektRegistration
    {
        public string? Office { get; set; }
        public string? Number { get; set; }
    }
}
