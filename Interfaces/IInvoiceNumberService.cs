using Fakturio.Api.Models;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceNumberService
{
    /// <summary>
    /// Sequential number for a document series: [prefix]YYYYNNNN
    /// (FA/empty for invoice, ZF for proforma, DOB for credit note).
    /// </summary>
    Task<string> NextAsync(
        InvoiceDocumentType documentType,
        string? invoicePrefix = null,
        string? proformaPrefix = null,
        string? creditNotePrefix = null,
        CancellationToken ct = default);
}
