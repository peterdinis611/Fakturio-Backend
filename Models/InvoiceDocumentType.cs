namespace Fakturio.Api.Models;

/// <summary>
/// SK document kinds with separate numbering series.
/// </summary>
public enum InvoiceDocumentType
{
    /// <summary>Daňový doklad (FA…).</summary>
    Invoice = 0,

    /// <summary>Zálohová / proforma (ZF…).</summary>
    Proforma = 1,

    /// <summary>Dobropis (DOB…) — create via credit-note endpoint.</summary>
    CreditNote = 2,

    /// <summary>Cenová ponuka (CP…).</summary>
    Quote = 3,

    /// <summary>Dodací list (DL…).</summary>
    DeliveryNote = 4,

    /// <summary>Objednávka / prijatá objednávka (OB…).</summary>
    SalesOrder = 5,
}
