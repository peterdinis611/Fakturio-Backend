using System.Net;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Compliance;

/// <summary>
/// SK Obchodný vestník — soft public HTML search (no stable JSON API).
/// </summary>
public sealed partial class SkObchodnyVestnikService(
    IHttpClientFactory httpClientFactory,
    ILogger<SkObchodnyVestnikService> logger) : ISkObchodnyVestnikService
{
    public async Task<ServiceResult<SkObchodnyVestnikResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<SkObchodnyVestnikResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        var portalUrl =
            $"https://obchodnyvestnik.justice.gov.sk/Search/Organization?Ico={Uri.EscapeDataString(normalized)}";

        try
        {
            var client = httpClientFactory.CreateClient("obchodnyvestnik");
            using var response = await client.GetAsync(
                $"Search/Organization?Ico={Uri.EscapeDataString(normalized)}",
                ct);
            var html = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Obchodný vestník returned {StatusCode}", (int)response.StatusCode);
                return SoftEmpty(normalized, portalUrl, "obchodny-vestnik-unavailable");
            }

            var found = html.Contains(normalized, StringComparison.Ordinal)
                        || IcoRegex().IsMatch(html);
            var name = ParseName(html);

            return ServiceResult<SkObchodnyVestnikResponse>.Ok(new SkObchodnyVestnikResponse(
                Ico: normalized,
                Found: found,
                Name: name,
                PortalUrl: portalUrl,
                Source: "obchodny-vestnik"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Obchodný vestník lookup failed for {Ico}", normalized);
            return SoftEmpty(normalized, portalUrl, "obchodny-vestnik-soft-fail");
        }
    }

    private static ServiceResult<SkObchodnyVestnikResponse> SoftEmpty(
        string ico,
        string portalUrl,
        string source)
        => ServiceResult<SkObchodnyVestnikResponse>.Ok(new SkObchodnyVestnikResponse(
            Ico: ico,
            Found: false,
            Name: null,
            PortalUrl: portalUrl,
            Source: source));

    private static string? ParseName(string html)
    {
        var match = NameRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    [GeneratedRegex(@"\b\d{8}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IcoRegex();

    [GeneratedRegex(@"<h\d[^>]*>([^<]{3,120})</h\d>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}