namespace Fakturio.Api.Models;

/// <summary>
/// SK document kinds with separate numbering series.
/// Invoice = daňový doklad, Proforma = zálohová/proforma, CreditNote = dobropis.
/// </summary>
public enum InvoiceDocumentType
{
    Invoice = 0,
    Proforma = 1,
    CreditNote = 2
}
