using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// ARES veřejný rejstřík — statutáři / prokura (REST, no key).
/// </summary>
public sealed class AresVrService(
    IHttpClientFactory httpClientFactory,
    ILogger<AresVrService> logger) : IAresVrService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<AresVrSummaryResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<AresVrSummaryResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("ares");
            using var response = await client.GetAsync(
                $"ekonomicke-subjekty-vr/{Uri.EscapeDataString(normalized)}",
                ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ServiceResult<AresVrSummaryResponse>.NotFound(
                    "Company not found in ARES VR.",
                    ErrorCodes.RegistryNotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "ARES VR returned {StatusCode} for {Ico}",
                    (int)response.StatusCode,
                    normalized);
                return Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<AresVrRoot>(JsonOptions, ct);
            var record = payload?.Zaznamy?
                .OrderByDescending(z => z.PrimarniZaznam)
                .FirstOrDefault();
            if (record is null)
            {
                return ServiceResult<AresVrSummaryResponse>.NotFound(
                    "Company not found in ARES VR.",
                    ErrorCodes.RegistryNotFound);
            }

            var name = CurrentValue(record.ObchodniJmeno);
            var court = FormatCourtFile(record.SpisovaZnacka?.FirstOrDefault(s => s.DatumVymazu is null)
                                        ?? record.SpisovaZnacka?.FirstOrDefault());
            var capital = FormatCapital(record.ZakladniKapital?
                .FirstOrDefault(k => k.DatumVymazu is null)
                ?? record.ZakladniKapital?.FirstOrDefault());

            var officers = new List<AresVrPersonItem>();
            foreach (var organ in record.StatutarniOrgany ?? [])
            {
                foreach (var member in organ.ClenoveOrganu ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(member.DatumVymazu))
                        continue;

                    var person = FormatPerson(member.FyzickaOsoba);
                    if (person is null)
                        continue;

                    var role = member.Clenstvi?.Funkce?.Nazev
                               ?? member.NazevAngazma
                               ?? organ.NazevOrganu;

                    officers.Add(new AresVrPersonItem(
                        Name: person,
                        Role: string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
                        Organ: string.IsNullOrWhiteSpace(organ.NazevOrganu) ? null : organ.NazevOrganu.Trim(),
                        Since: member.Clenstvi?.Funkce?.VznikFunkce
                               ?? member.Clenstvi?.Clenstvi?.VznikClenstvi
                               ?? member.DatumZapisu));
                }
            }

            return ServiceResult<AresVrSummaryResponse>.Ok(new AresVrSummaryResponse(
                Ico: normalized,
                Name: name,
                CourtFile: court,
                RegisteredCapital: capital,
                Officers: officers,
                OfficersCount: officers.Count,
                Source: "ares-vr"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ARES VR lookup failed for {Ico}", normalized);
            return Unavailable();
        }
    }

    private static ServiceResult<AresVrSummaryResponse> Unavailable() =>
        ServiceResult<AresVrSummaryResponse>.Fail(
            "ARES VR is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.AresVrUnavailable);

    private static string? CurrentValue(List<DatedValue>? values)
    {
        var current = values?.FirstOrDefault(v => string.IsNullOrWhiteSpace(v.DatumVymazu))
                      ?? values?.FirstOrDefault();
        return string.IsNullOrWhiteSpace(current?.Hodnota) ? null : current.Hodnota.Trim();
    }

    private static string? FormatCourtFile(CourtFile? file)
    {
        if (file is null)
            return null;
        var parts = new[] { file.Soud, file.Oddil, file.Vlozka?.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? FormatCapital(CapitalEntry? capital)
    {
        var raw = capital?.Vklad?.Hodnota;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var normalized = raw.Replace(';', '.').Trim();
        if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
        {
            var unit = capital?.Vklad?.TypObnos?.Equals("KORUNY", StringComparison.OrdinalIgnoreCase) == true
                ? " Kč"
                : "";
            return $"{amount:N0}{unit}";
        }
        return raw;
    }

    private static string? FormatPerson(PhysicalPerson? person)
    {
        if (person is null)
            return null;
        var parts = new[] { person.TitulPredJmenem, person.Jmeno, person.Prijmeni }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim());
        var joined = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private sealed class AresVrRoot
    {
        public List<AresVrRecord>? Zaznamy { get; set; }
    }

    private sealed class AresVrRecord
    {
        public bool PrimarniZaznam { get; set; }
        public List<DatedValue>? ObchodniJmeno { get; set; }
        public List<CourtFile>? SpisovaZnacka { get; set; }
        public List<CapitalEntry>? ZakladniKapital { get; set; }
        public List<OrganEntry>? StatutarniOrgany { get; set; }
    }

    private sealed class DatedValue
    {
        public string? Hodnota { get; set; }
        public string? DatumVymazu { get; set; }
    }

    private sealed class CourtFile
    {
        public string? Soud { get; set; }
        public string? Oddil { get; set; }
        public int? Vlozka { get; set; }
        public string? DatumVymazu { get; set; }
    }

    private sealed class CapitalEntry
    {
        public MoneyAmount? Vklad { get; set; }
        public string? DatumVymazu { get; set; }
    }

    private sealed class MoneyAmount
    {
        public string? TypObnos { get; set; }
        public string? Hodnota { get; set; }
    }

    private sealed class OrganEntry
    {
        public string? NazevOrganu { get; set; }
        public List<OrganMember>? ClenoveOrganu { get; set; }
    }

    private sealed class OrganMember
    {
        public string? DatumZapisu { get; set; }
        public string? DatumVymazu { get; set; }
        public string? NazevAngazma { get; set; }
        public MembershipBlock? Clenstvi { get; set; }
        public PhysicalPerson? FyzickaOsoba { get; set; }
    }

    private sealed class MembershipBlock
    {
        public MembershipDates? Clenstvi { get; set; }
        public FunctionBlock? Funkce { get; set; }
    }

    private sealed class MembershipDates
    {
        public string? VznikClenstvi { get; set; }
    }

    private sealed class FunctionBlock
    {
        public string? Nazev { get; set; }
        public string? VznikFunkce { get; set; }
    }

    private sealed class PhysicalPerson
    {
        public string? Jmeno { get; set; }
        public string? Prijmeni { get; set; }
        [JsonPropertyName("titulPredJmenem")]
        public string? TitulPredJmenem { get; set; }
    }
}
