using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Iban;

public sealed class IbanLookupService(
    IHttpClientFactory httpClientFactory,
    ILogger<IbanLookupService> logger) : IIbanLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, IbanLookupResponse> Cache = new(StringComparer.Ordinal);

    /// <summary>NBS bank codes → name (common SK codes).</summary>
    private static readonly Dictionary<string, (string Name, string? Bic)> SkBanks = new(StringComparer.Ordinal)
    {
        ["0200"] = ("Slovenská sporiteľňa", "GIBASKBX"),
        ["0720"] = ("Národná banka Slovenska", "NBSBSKBX"),
        ["0900"] = ("Slovenská pošta / Poštová banka", "PBNKSKBA"),
        ["1100"] = ("Tatra banka", "TATRSKBX"),
        ["1111"] = ("UniCredit Bank CZ/SK", "UNCRSKBX"),
        ["1200"] = ("VÚB banka", "SUBASKBX"),
        ["3000"] = ("Slovenská záručná a rozvojová banka", "SLZBSKBA"),
        ["3100"] = ("Prima banka Slovensko", "KOMASK2X"),
        ["5200"] = ("OTP Banka Slovensko", "OTPVSKBX"),
        ["5600"] = ("Prima banka Slovensko", "KOMASK2X"),
        ["5900"] = ("Prima banka Slovensko", "KOMASK2X"),
        ["6500"] = ("OTP Banka", "OTPVSKBX"),
        ["7300"] = ("ING Bank", "INGBSKBX"),
        ["7500"] = ("J&T BANKA", "JTBPSKBA"),
        ["7930"] = ("Wüstenrot", "WUSTSKBA"),
        ["8100"] = ("ČSOB", "CEKOSKBX"),
        ["8120"] = ("UniCredit Bank", "UNCRSKBX"),
        ["8130"] = ("Citibank Europe", "CITISKBA"),
        ["8160"] = ("EXIMBANKA SR", "EXSKSKBX"),
        ["8170"] = ("ČSOB", "CEKOSKBX"),
        ["8180"] = ("Štátna pokladnica", "SPSRSKBA"),
        ["8320"] = ("Fio banka", "FIOZSKBA"),
        ["8330"] = ("Fio banka", "FIOZSKBA"),
        ["8350"] = ("J&T BANKA", "JTBPSKBA"),
        ["8360"] = ("mBank", "BREXSKBX"),
        ["8370"] = ("Oberbank", "OBKLSKBA"),
        ["8380"] = ("BKS Bank", "BFKKSKBB"),
        ["8390"] = ("Privatbanka", "BSLOSK22"),
        ["8410"] = ("Raiffeisen Bank", "TATRSKBX"),
        ["8420"] = ("Prima banka", "KOMASK2X"),
        ["8430"] = ("Komerční banka Bratislava", "KOMBSKBA"),
        ["9950"] = ("First Data / Card", null),
        ["9951"] = ("Diners Club", null),
        ["9952"] = ("Home Credit", null),
    };

    /// <summary>Common CZ bank codes (ČNB).</summary>
    private static readonly Dictionary<string, (string Name, string? Bic)> CzBanks = new(StringComparer.Ordinal)
    {
        ["0100"] = ("Komerční banka", "KOMBCZPP"),
        ["0300"] = ("ČSOB", "CEKOCZPP"),
        ["0600"] = ("MONETA Money Bank", "AGBACZPP"),
        ["0710"] = ("Česká národní banka", "CNBACZPP"),
        ["0800"] = ("Česká spořitelna", "GIBACZPX"),
        ["2010"] = ("Fio banka", "FIOBCZPP"),
        ["2060"] = ("Citfin", "CITFCZPP"),
        ["2070"] = ("TRINITY BANK", "MPUBCZPP"),
        ["2100"] = ("UniCredit Bank", "BACXCZPP"),
        ["2200"] = ("Peněžní dům", null),
        ["2220"] = ("Artesa", "ARTTCZPP"),
        ["2240"] = ("Poštová banka", "POBNCZPP"),
        ["2250"] = ("Banka CREDITAS", "CTASCZ22"),
        ["2260"] = ("NEY spořitelní družstvo", null),
        ["2310"] = ("ZUNO BANK", "ZUNOCZPP"),
        ["2600"] = ("Citibank Europe", "CITICZPX"),
        ["2700"] = ("UniCredit Bank", "BACXCZPP"),
        ["3030"] = ("Air Bank", "AIRACZPP"),
        ["3050"] = ("BNP Paribas Personal Finance", "BPPFCZP1"),
        ["3060"] = ("PKO BP", null),
        ["3500"] = ("ING Bank", "INGBCZPP"),
        ["4000"] = ("Expobank CZ", "EXPNCZPP"),
        ["4300"] = ("Českomoravská záruční a rozvojová banka", "CMZRCZP1"),
        ["5500"] = ("Raiffeisenbank", "RZBCCZPP"),
        ["5800"] = ("J&T BANKA", "JTBPCZPP"),
        ["6000"] = ("PPF banka", "PMBPCZPP"),
        ["6100"] = ("Equa bank", "EQBKCZPP"),
        ["6200"] = ("COMMERZBANK Aktiengesellschaft", "COBACZPX"),
        ["6210"] = ("mBank", "BREXCZPP"),
        ["6300"] = ("BNP Paribas", "GEBACZPP"),
        ["6700"] = ("Všeobecná úverová banka", null),
        ["6800"] = ("Sberbank CZ", "VBOECZ2X"),
        ["7910"] = ("Deutsche Bank", "DEUTCZPX"),
        ["7940"] = ("Waldviertler Sparkasse Bank", null),
        ["7950"] = ("Raiffeisen stavební spořitelna", null),
        ["7960"] = ("Českomoravská stavební spořitelna", null),
        ["7970"] = ("Wüstenrot hypoteční banka", null),
        ["7980"] = ("Wüstenrot stavební spořitelna", null),
        ["7990"] = ("Modrá pyramida stavební spořitelna", null),
        ["8030"] = ("Volksbank Raiffeisenbank Nordoberpfalz", null),
        ["8040"] = ("Oberbank AG", "OBKLCZ2X"),
        ["8060"] = ("Stavební spořitelna České spořitelny", null),
        ["8090"] = ("Česká exportní banka", "CZEECZPP"),
        ["8150"] = ("HSBC Bank", "MIDLCZPP"),
        ["8200"] = ("PRIVAT BANK", "PRIBCZPP"),
    };

    public async Task<ServiceResult<IbanLookupResponse>> LookupAsync(
        string iban,
        CancellationToken ct = default)
    {
        var normalized = (iban ?? string.Empty)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        if (normalized.Length < 15 || !SlovakValidators.BeValidIban(normalized))
        {
            return ServiceResult<IbanLookupResponse>.BadRequest(
                "IBAN is invalid.",
                ErrorCodes.InvalidIban);
        }

        if (Cache.TryGetValue(normalized, out var cached))
            return ServiceResult<IbanLookupResponse>.Ok(cached);

        var country = normalized[..2];
        string? bankCode = null;
        string? bankName = null;
        string? bic = null;
        string? city = null;
        var source = "local";

        if (normalized.Length >= 8)
        {
            bankCode = normalized.Substring(4, 4);
            if (country == "SK" && SkBanks.TryGetValue(bankCode, out var sk))
            {
                bankName = sk.Name;
                bic = sk.Bic;
            }
            else if (country == "CZ" && CzBanks.TryGetValue(bankCode, out var cz))
            {
                bankName = cz.Name;
                bic = cz.Bic;
            }
        }

        try
        {
            var client = httpClientFactory.CreateClient("openiban");
            var url =
                $"validate/{Uri.EscapeDataString(normalized)}?getBIC=true&validateBankCode=true";
            using var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<OpenIbanPayload>(JsonOptions, ct);
                if (payload is not null)
                {
                    if (!string.IsNullOrWhiteSpace(payload.BankData?.Name))
                    {
                        bankName = payload.BankData.Name.Trim();
                        source = "openiban";
                    }

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.Bic))
                        bic = payload.BankData.Bic.Trim().ToUpperInvariant();

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.BankCode))
                        bankCode = payload.BankData.BankCode.Trim();

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.City))
                        city = payload.BankData.City.Trim();

                    if (source == "local" && !string.IsNullOrWhiteSpace(bankName))
                        source = "local+openiban";
                    else if (source == "local" && payload.Valid)
                        source = string.IsNullOrWhiteSpace(bankName) ? "local" : source;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "openiban enrichment failed for {Iban}", normalized);
        }

        var result = new IbanLookupResponse(
            IsValid: true,
            Iban: normalized,
            CountryCode: country,
            BankCode: bankCode,
            BankName: bankName,
            Bic: bic,
            City: city,
            Source: source);

        Cache[normalized] = result;
        return ServiceResult<IbanLookupResponse>.Ok(result);
    }

    private sealed class OpenIbanPayload
    {
        public bool Valid { get; set; }
        public OpenIbanBankData? BankData { get; set; }
    }

    private sealed class OpenIbanBankData
    {
        public string? BankCode { get; set; }
        public string? Name { get; set; }
        public string? Bic { get; set; }
        public string? City { get; set; }
        public string? Zip { get; set; }
    }
}
