using Fakturio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services;

public class InvoiceNumberService(AppDbContext db)
{
    /// <summary>
    /// Generates sequential invoice numbers in format YYYYNNNN (e.g. 20260001).
    /// </summary>
    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"{year}";

        var lastNumber = await db.Invoices
            .Where(i => i.Number.StartsWith(prefix))
            .OrderByDescending(i => i.Number)
            .Select(i => i.Number)
            .FirstOrDefaultAsync(ct);

        var sequence = 1;
        if (lastNumber is not null && lastNumber.Length >= 8
            && int.TryParse(lastNumber[4..], out var parsed))
        {
            sequence = parsed + 1;
        }

        return $"{year}{sequence:D4}";
    }
}
