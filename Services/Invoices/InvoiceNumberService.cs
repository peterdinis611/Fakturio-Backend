using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public class InvoiceNumberService(AppDbContext db) : IInvoiceNumberService
{
    /// <summary>
    /// Generates sequential invoice numbers: [prefix]YYYYNNNN (e.g. FA20260001).
    /// </summary>
    public async Task<string> NextAsync(string? prefix = null, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.Trim();
        var yearPart = $"{year}";
        var startsWith = $"{cleanPrefix}{yearPart}";

        var lastNumber = await db.Invoices
            .Where(i => i.Number.StartsWith(startsWith))
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
}
