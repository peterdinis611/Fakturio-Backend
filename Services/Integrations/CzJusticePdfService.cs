using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// Deep links to CZ Justice / obchodní rejstřík public extracts (no binary scrape).
/// </summary>
public sealed class CzJusticePdfService(IOrsrService? orsr = null) : ICzJusticePdfService
{
    public async Task<ServiceResult<CzJusticePdfResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<CzJusticePdfResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
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
                "ISIR — insolvenční rejstřík",
                $"https://isir.justice.cz/isir/ueu/vysledek_lustrace.do?nazev_osoby=&ic={normalized}&mesto=&cislo_senatu=&bc_vec=&rocnik=&id_osoby_puvodce=&druh_stav_konkursu=&datum_stav_od=&datum_stav_do=&aktualnost=AKTUALNI_I_UKONCENA"),
        };

        if (orsr is not null)
        {
            try
            {
                var orsrResult = await orsr.LookupByIcoAsync(normalized, ct);
                if (orsrResult.Success && orsrResult.Data is { } data)
                {
                    foreach (var doc in data.Documents.Take(5))
                    {
                        if (!string.IsNullOrWhiteSpace(doc.Url))
                        {
                            links.Add(new(
                                doc.Title ?? "ORSR dokument",
                                doc.Url!));
                        }
                    }
                }
            }
            catch
            {
                // ignore — deep links above are enough
            }
        }

        return ServiceResult<CzJusticePdfResponse>.Ok(new(
            normalized,
            true,
            links,
            "cz-justice-links"));
    }
}
