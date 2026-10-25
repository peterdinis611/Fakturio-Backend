using Fakturio.Api.Models;

namespace Fakturio.Api.Services;

public static class InvoiceCalculator
{
    public static void RecalculateTotals(Invoice invoice)
    {
        decimal subtotal = 0;
        decimal vatTotal = 0;

        foreach (var item in invoice.Items)
        {
            subtotal += item.Subtotal;
            vatTotal += item.VatAmount;
        }

        invoice.Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        invoice.VatTotal = Math.Round(vatTotal, 2, MidpointRounding.AwayFromZero);
        invoice.Total = invoice.Subtotal + invoice.VatTotal;
        invoice.UpdatedAt = DateTime.UtcNow;
    }
}
