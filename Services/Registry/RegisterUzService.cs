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
/// Register účtovných závierok (RÚZ) — SK accounting unit + statements (REST, no key).
/// </summary>
public sealed class RegisterUzService(
    IHttpClientFactory httpClientFactory,
    ILogger<RegisterUzService> logger) : IRegisterUzService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<RegisterUzUnitResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<RegisterUzUnitResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("registeruz");
            var unitResult = await ResolveUnitAsync(client, normalized, ct);
            if (!unitResult.Success)
                return unitResult.Error!;

            var unit = unitResult.Unit!;
            return ServiceResult<RegisterUzUnitResponse>.Ok(new RegisterUzUnitResponse(
                Ico: unit.Ico?.Trim() ?? normalized,
                Dic: string.IsNullOrWhiteSpace(unit.Dic) ? null : unit.Dic.Trim(),
                Name: unit.NazovUJ!.Trim(),
                Street: string.IsNullOrWhiteSpace(unit.Ulica) ? null : unit.Ulica.Trim(),
                City: string.IsNullOrWhiteSpace(unit.Mesto) ? null : unit.Mesto.Trim(),
                PostalCode: NormalizePostal(unit.Psc),
                FoundedAt: string.IsNullOrWhiteSpace(unit.DatumZalozenia) ? null : unit.DatumZalozenia.Trim(),
                CancelledAt: string.IsNullOrWhiteSpace(unit.DatumZrusenia) ? null : unit.DatumZrusenia.Trim(),
                StatementsCount: unit.IdUctovnychZavierok?.Count ?? 0,
                AnnualReportsCount: unit.IdVyrocnychSprav?.Count ?? 0,
                Source: "registeruz"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RegisterUZ lookup failed for {Ico}", normalized);
            return UnavailableUnit();
        }
    }

    public async Task<ServiceResult<RegisterUzStatementsResponse>> LookupStatementsByIcoAsync(
        string ico,
        int take = 5,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<RegisterUzStatementsResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        var limit = Math.Clamp(take, 1, 10);

        try
        {
            var client = httpClientFactory.CreateClient("registeruz");
            var unitResult = await ResolveUnitAsync(client, normalized, ct);
            if (!unitResult.Success)
            {
                return unitResult.ErrorCode == ErrorCodes.RegistryNotFound
                    ? ServiceResult<RegisterUzStatementsResponse>.NotFound(
                        "Accounting unit not found in RegisterUZ.",
                        ErrorCodes.RegistryNotFound)
                    : UnavailableStatements();
            }

            var unit = unitResult.Unit!;
            var statementIds = (unit.IdUctovnychZavierok ?? [])
                .OrderByDescending(id => id)
                .Take(limit)
                .ToList();

            var items = new List<RegisterUzStatementItem>();
            foreach (var statementId in statementIds)
            {
                using var stmtResponse = await client.GetAsync($"uctovna-zavierka?id={statementId}", ct);
                if (!stmtResponse.IsSuccessStatusCode)
                    continue;

                var stmt = await stmtResponse.Content.ReadFromJsonAsync<StatementPayload>(JsonOptions, ct);
                if (stmt is null)
                    continue;

                items.Add(new RegisterUzStatementItem(
                    Id: stmt.Id,
                    Type: string.IsNullOrWhiteSpace(stmt.Typ) ? null : stmt.Typ.Trim(),
                    PeriodFrom: string.IsNullOrWhiteSpace(stmt.ObdobieOd) ? null : stmt.ObdobieOd.Trim(),
                    PeriodTo: string.IsNullOrWhiteSpace(stmt.ObdobieDo) ? null : stmt.ObdobieDo.Trim(),
                    AssembledAt: string.IsNullOrWhiteSpace(stmt.DatumZostavenia) ? null : stmt.DatumZostavenia.Trim(),
                    FiledAt: string.IsNullOrWhiteSpace(stmt.DatumPodania) ? null : stmt.DatumPodania.Trim(),
                    ReportsCount: stmt.IdUctovnychVykazov?.Count ?? 0));
            }

            return ServiceResult<RegisterUzStatementsResponse>.Ok(new RegisterUzStatementsResponse(
                Ico: unit.Ico?.Trim() ?? normalized,
                Name: unit.NazovUJ?.Trim(),
                Statements: items,
                Source: "registeruz"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RegisterUZ statements lookup failed for {Ico}", normalized);
            return UnavailableStatements();
        }
    }

    private async Task<(bool Success, UnitPayload? Unit, string? ErrorCode, ServiceResult<RegisterUzUnitResponse>? Error)> ResolveUnitAsync(
        HttpClient client,
        string normalizedIco,
        CancellationToken ct)
    {
        var listUrl =
            $"uctovne-jednotky?zmenene-od=2000-01-01&max-zaznamov=5&ico={Uri.EscapeDataString(normalizedIco)}";
        using var listResponse = await client.GetAsync(listUrl, ct);

        if (!listResponse.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "RegisterUZ list returned {StatusCode} for {Ico}",
                (int)listResponse.StatusCode,
                normalizedIco);
            return (false, null, ErrorCodes.RegisterUzUnavailable, UnavailableUnit());
        }

        var list = await listResponse.Content.ReadFromJsonAsync<IdListPayload>(JsonOptions, ct);
        var id = list?.Id?.FirstOrDefault();
        if (id is null or 0)
        {
            return (false, null, ErrorCodes.RegistryNotFound,
                ServiceResult<RegisterUzUnitResponse>.NotFound(
                    "Accounting unit not found in RegisterUZ.",
                    ErrorCodes.RegistryNotFound));
        }

        using var detailResponse = await client.GetAsync($"uctovna-jednotka?id={id}", ct);
        if (!detailResponse.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "RegisterUZ detail returned {StatusCode} for id {Id}",
                (int)detailResponse.StatusCode,
                id);
            return (false, null, ErrorCodes.RegisterUzUnavailable, UnavailableUnit());
        }

        var unit = await detailResponse.Content.ReadFromJsonAsync<UnitPayload>(JsonOptions, ct);
        if (unit is null || string.IsNullOrWhiteSpace(unit.NazovUJ))
        {
            return (false, null, ErrorCodes.RegistryNotFound,
                ServiceResult<RegisterUzUnitResponse>.NotFound(
                    "Accounting unit not found in RegisterUZ.",
                    ErrorCodes.RegistryNotFound));
        }

        return (true, unit, null, null);
    }

    private static ServiceResult<RegisterUzUnitResponse> UnavailableUnit() =>
        ServiceResult<RegisterUzUnitResponse>.Fail(
            "RegisterUZ is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.RegisterUzUnavailable);

    private static ServiceResult<RegisterUzStatementsResponse> UnavailableStatements() =>
        ServiceResult<RegisterUzStatementsResponse>.Fail(
            "RegisterUZ is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.RegisterUzUnavailable);

    private static string? NormalizePostal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 5)
            return $"{digits[..3]} {digits[3..]}";
        return raw.Trim();
    }

    private sealed class IdListPayload
    {
        public List<long>? Id { get; set; }
        [JsonPropertyName("existujeDalsieId")]
        public bool ExistujeDalsieId { get; set; }
    }

    private sealed class UnitPayload
    {
        public long Id { get; set; }
        public string? Ico { get; set; }
        public string? Dic { get; set; }
        public string? NazovUJ { get; set; }
        public string? Ulica { get; set; }
        public string? Mesto { get; set; }
        public string? Psc { get; set; }
        public string? DatumZalozenia { get; set; }
        public string? DatumZrusenia { get; set; }
        public List<long>? IdUctovnychZavierok { get; set; }
        public List<long>? IdVyrocnychSprav { get; set; }
    }

    private sealed class StatementPayload
    {
        public long Id { get; set; }
        public string? Typ { get; set; }
        public string? ObdobieOd { get; set; }
        public string? ObdobieDo { get; set; }
        public string? DatumZostavenia { get; set; }
        public string? DatumPodania { get; set; }
        public List<long>? IdUctovnychVykazov { get; set; }
    }
}
