using System.Net;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// Registr smluv (ISRS) — CZ public contracts by counterparty IČO.
/// Official open search is HTML-only; we soft-parse the public portal (no key).
/// </summary>
public sealed partial class RegistrSmluvService(
    IHttpClientFactory httpClientFactory,
    ILogger<RegistrSmluvService> logger) : IRegistrSmluvService
{
    public async Task<ServiceResult<RegistrSmluvStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<RegistrSmluvStatusResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        var portalUrl = $"https://smlouvy.gov.cz/vyhledavani?party_idnum={normalized}";

        try
        {
            var client = httpClientFactory.CreateClient("registrsmluv");
            using var response = await client.GetAsync(
                $"vyhledavani?party_idnum={Uri.EscapeDataString(normalized)}",
                ct);
            var html = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Registr smluv returned {StatusCode} for {Ico}",
                    (int)response.StatusCode,
                    normalized);
                return Unavailable();
            }

            var total = ParseTotal(html);
            var recent = ParseRows(html).Take(8).ToList();

            return ServiceResult<RegistrSmluvStatusResponse>.Ok(new RegistrSmluvStatusResponse(
                Ico: normalized,
                Found: total > 0 || recent.Count > 0,
                TotalCount: total > 0 ? total : recent.Count,
                PortalUrl: portalUrl,
                Recent: recent,
                Source: "registr-smluv"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Registr smluv lookup failed for {Ico}", normalized);
            return Unavailable();
        }
    }

    private static int ParseTotal(string html)
    {
        var match = TotalRegex().Match(html);
        if (!match.Success)
            return 0;

        var digits = new string(match.Groups[1].Value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    private static IEnumerable<RegistrSmluvContractItem> ParseRows(string html)
    {
        foreach (Match row in RowRegex().Matches(html))
        {
            var cells = CellRegex().Matches(row.Value)
                .Select(m => Clean(m.Groups[1].Value))
                .Where(c => c.Length > 0)
                .ToList();

            if (cells.Count < 5)
                continue;

            var detail = DetailRegex().Match(row.Value);
            var detailUrl = detail.Success
                ? "https://smlouvy.gov.cz" + WebUtility.HtmlDecode(detail.Groups[1].Value)
                : null;

            // Columns: subject, description, lastVersion, published, value, party[, detail]
            yield return new RegistrSmluvContractItem(
                SubjectName: cells.ElementAtOrDefault(0),
                Description: cells.ElementAtOrDefault(1),
                PublishedAt: cells.ElementAtOrDefault(3),
                Value: cells.ElementAtOrDefault(4),
                PartyName: cells.ElementAtOrDefault(5),
                DetailUrl: detailUrl);
        }
    }

    private static string Clean(string raw)
    {
        var noTags = TagRegex().Replace(WebUtility.HtmlDecode(raw), " ");
        return WhitespaceRegex().Replace(noTags, " ").Trim();
    }

    private static ServiceResult<RegistrSmluvStatusResponse> Unavailable() =>
        ServiceResult<RegistrSmluvStatusResponse>.Fail(
            "Registr smluv is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.RegistrSmluvUnavailable);

    [GeneratedRegex(@"Po[čc]et\s+nalezn[ýy]ch\s+z[áa]znam[ůu]\s*([0-9\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TotalRegex();

    [GeneratedRegex(@"<tr>\s*<td[\s\S]*?</tr>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RowRegex();

    [GeneratedRegex(@"<td[^>]*>([\s\S]*?)</td>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CellRegex();

    [GeneratedRegex(@"href=""(/smlouva/\d+[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DetailRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
