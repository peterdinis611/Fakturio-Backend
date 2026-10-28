using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public class InvoiceNumberService(AppDbContext db) : IInvoiceNumberService
{
    public async Task<string> NextAsync(
        InvoiceDocumentType documentType,
        string? invoicePrefix = null,
        string? proformaPrefix = null,
        string? creditNotePrefix = null,
        CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var cleanPrefix = ResolvePrefix(documentType, invoicePrefix, proformaPrefix, creditNotePrefix);
        var yearPart = $"{year}";
        var startsWith = $"{cleanPrefix}{yearPart}";

        var lastNumber = await db.Invoices
            .Where(i => i.DocumentType == documentType && i.Number.StartsWith(startsWith))
            .OrderByDescending(i => i.Number)
            .Select(i => i.Number)
            .FirstOrDefaultAsync(ct);

        var sequence = 1;
        if (lastNumber is not null)
        {
            var digits = new string(lastNumber.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (digits.Length >= 4 && int.TryParse(digits[^4..], out var parsed))
                sequence = parsed + 1;
            else if (int.TryParse(digits, out parsed))
                sequence = parsed + 1;
        }

        return $"{cleanPrefix}{yearPart}{sequence:D4}";
    }

    private static string ResolvePrefix(
        InvoiceDocumentType documentType,
        string? invoicePrefix,
        string? proformaPrefix,
        string? creditNotePrefix) =>
        documentType switch
        {
            InvoiceDocumentType.Proforma =>
                string.IsNullOrWhiteSpace(proformaPrefix) ? "ZF" : proformaPrefix.Trim(),
            InvoiceDocumentType.CreditNote =>
                string.IsNullOrWhiteSpace(creditNotePrefix) ? "DOB" : creditNotePrefix.Trim(),
            _ => string.IsNullOrWhiteSpace(invoicePrefix) ? string.Empty : invoicePrefix.Trim()
        };
}
