using Fakturio.Api.Models;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceNumberService
{
    Task<string> NextAsync(
        Guid companyId,
        InvoiceDocumentType documentType,
        string? invoicePrefix = null,
        string? proformaPrefix = null,
        string? creditNotePrefix = null,
        CancellationToken ct = default);
}
