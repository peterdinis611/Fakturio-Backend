using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// RPVS OpenData v2 — SK register of public-sector partners (OData, no key).
/// </summary>
public sealed class RpvsService(
    IHttpClientFactory httpClientFactory,
    ILogger<RpvsService> logger) : IRpvsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<RpvsStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<RpvsStatusResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("rpvs");
            var filter = Uri.EscapeDataString($"Ico eq '{normalized}'");
            var url = $"PartneriVerejnehoSektora?$filter={filter}&$expand=Partner";

            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "RPVS returned {StatusCode} for {Ico}",
                    (int)response.StatusCode,
                    normalized);
                return Unavailable();
            }

            var payload = await response.Content.ReadFromJsonAsync<ODataList<PartnerEntity>>(
                JsonOptions,
                ct);
            var rows = payload?.Value ?? [];
            var now = DateTimeOffset.UtcNow;

            var registrations = rows
                .Select(r =>
                {
                    var validTo = ParseOffset(r.PlatnostDo);
                    var isActive = validTo is null || validTo > now;
                    return new RpvsRegistrationItem(
                        Id: r.Id,
                        Name: NullIfEmpty(r.ObchodneMeno),
                        ValidFrom: FormatDate(r.PlatnostOd),
                        ValidTo: FormatDate(r.PlatnostDo),
                        IsActive: isActive,
                        PartnerId: r.Partner?.Id,
                        FileNumber: r.Partner?.CisloVlozky);
                })
                .OrderByDescending(r => r.IsActive)
                .ThenByDescending(r => r.ValidFrom)
                .ToList();

            var active = registrations.FirstOrDefault(r => r.IsActive) ?? registrations.FirstOrDefault();
            var partnerId = active?.PartnerId;
            var owners = partnerId is long pid
                ? await LoadOwnersAsync(client, pid, ct)
                : [];

            return ServiceResult<RpvsStatusResponse>.Ok(new RpvsStatusResponse(
                Ico: normalized,
                IsRegistered: registrations.Any(r => r.IsActive),
                Name: active?.Name ?? registrations.FirstOrDefault()?.Name,
                PartnerId: partnerId,
                FileNumber: active?.FileNumber,
                PortalUrl: partnerId is long id
                    ? $"https://rpvs.gov.sk/rpvs/Partner/Partner/Detail/{id}"
                    : "https://rpvs.gov.sk/",
                Registrations: registrations,
                BeneficialOwners: owners,
                Source: "rpvs"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RPVS lookup failed for {Ico}", normalized);
            return Unavailable();
        }
    }

    private async Task<IReadOnlyList<RpvsOwnerItem>> LoadOwnersAsync(
        HttpClient client,
        long partnerId,
        CancellationToken ct)
    {
        try
        {
            var filter = Uri.EscapeDataString($"Partner/Id eq {partnerId}");
            using var response = await client.GetAsync(
                $"KonecniUzivateliaVyhod?$filter={filter}",
                ct);
            if (!response.IsSuccessStatusCode)
                return [];

            var payload = await response.Content.ReadFromJsonAsync<ODataList<OwnerEntity>>(
                JsonOptions,
                ct);
            var now = DateTimeOffset.UtcNow;

            return (payload?.Value ?? [])
                .Where(o =>
                {
                    var validTo = ParseOffset(o.PlatnostDo);
                    return validTo is null || validTo > now;
                })
                .Select(o =>
                {
                    var name = string.Join(
                        " ",
                        new[] { o.TitulPred, o.Meno, o.Priezvisko, o.TitulZa, o.ObchodneMeno }
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                        .Trim();
                    return new RpvsOwnerItem(
                        Name: string.IsNullOrWhiteSpace(name) ? "—" : name,
                        IsPublicOfficial: o.JeVerejnyCinitel,
                        ValidFrom: FormatDate(o.PlatnostOd),
                        ValidTo: FormatDate(o.PlatnostDo));
                })
                .Take(20)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RPVS owners lookup failed for partner {PartnerId}", partnerId);
            return [];
        }
    }

    private static ServiceResult<RpvsStatusResponse> Unavailable() =>
        ServiceResult<RpvsStatusResponse>.Fail(
            "RPVS is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.RpvsUnavailable);

    private static DateTimeOffset? ParseOffset(string? value) =>
        DateTimeOffset.TryParse(value, out var dt) ? dt : null;

    private static string? FormatDate(string? value) =>
        DateTimeOffset.TryParse(value, out var dt) ? dt.ToString("yyyy-MM-dd") : NullIfEmpty(value);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ODataList<T>
    {
        public List<T>? Value { get; set; }
    }

    private sealed class PartnerEntity
    {
        public long Id { get; set; }
        public string? ObchodneMeno { get; set; }
        public string? Ico { get; set; }
        public string? PlatnostOd { get; set; }
        public string? PlatnostDo { get; set; }
        public PartnerNav? Partner { get; set; }
    }

    private sealed class PartnerNav
    {
        public long Id { get; set; }
        public int? CisloVlozky { get; set; }
    }

    private sealed class OwnerEntity
    {
        public string? Meno { get; set; }
        public string? Priezvisko { get; set; }
        public string? TitulPred { get; set; }
        public string? TitulZa { get; set; }
        public string? ObchodneMeno { get; set; }
        public bool JeVerejnyCinitel { get; set; }
        public string? PlatnostOd { get; set; }
        public string? PlatnostDo { get; set; }
    }
}
