using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// Deep links to CZ Justice / obchodní rejstřík public extracts (no binary scrape).
/// </summary>
public sealed class CzJusticePdfService : ICzJusticePdfService
{
    public Task<ServiceResult<CzJusticePdfResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return Task.FromResult(ServiceResult<CzJusticePdfResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco));
        }

        var links = new List<CzJusticeLinkItem>
        {
            new(
                "Obchodní rejstřík — vyhledání podle IČO",
                $"https://or.justice.cz/ias/ui/rejstrik-$firma?ico={normalized}"),
            new(
                "ARES — ekonomický subjekt",
                $"https://ares.gov.cz/ekonomicke-subjekty?ico={normalized}"),
            new(
                "ISIR — insolvenční rejstřík (lustrace)",
                $"https://isir.justice.cz/isir/ueu/vysledek_lustrace.do?ic={normalized}&aktualnost=AKTUALNI_I_UKONCENA"),
            new(
                "Justice.cz — fulltext IČO",
                $"https://www.justice.cz/web/msp/obchodni-rejstrik?ico={normalized}"),
        };

        return Task.FromResult(ServiceResult<CzJusticePdfResponse>.Ok(new(
            normalized,
            true,
            links,
            "cz-justice-links")));
    }
}
