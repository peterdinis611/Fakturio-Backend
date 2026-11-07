using Fakturio.Api.Models;
using Fakturio.Api.Services.Invoices;

namespace Fakturio.Api.Tests;

public class InvoiceCalculatorTests
{
    [Fact]
    public void RecalculateTotals_sums_line_subtotals_and_vat_with_away_from_zero()
    {
        var invoice = new Invoice
        {
            Items =
            [
                new InvoiceItem
                {
                    Description = "Design",
                    Quantity = 2,
                    UnitPrice = 100.125m,
                    VatRate = 23m,
                },
                new InvoiceItem
                {
                    Description = "Hosting",
                    Quantity = 1,
                    UnitPrice = 49.995m,
                    VatRate = 23m,
                },
            ],
        };

        InvoiceCalculator.RecalculateTotals(invoice);

        // Line 1: 2 * 100.125 = 200.25; vat = round(200.25 * 0.23, 2) = 46.06
        // Line 2: 1 * 49.995 = 50.00 (AwayFromZero); vat = round(50 * 0.23, 2) = 11.50
        var lines = invoice.Items.ToList();
        Assert.Equal(200.25m, lines[0].Subtotal);
        Assert.Equal(46.06m, lines[0].VatAmount);
        Assert.Equal(50.00m, lines[1].Subtotal);
        Assert.Equal(11.50m, lines[1].VatAmount);

        Assert.Equal(250.25m, invoice.Subtotal);
        Assert.Equal(57.56m, invoice.VatTotal);
        Assert.Equal(307.81m, invoice.Total);
    }

    [Fact]
    public void RecalculateTotals_zero_items_yields_zero_totals()
    {
        var invoice = new Invoice();
        InvoiceCalculator.RecalculateTotals(invoice);

        Assert.Equal(0m, invoice.Subtotal);
        Assert.Equal(0m, invoice.VatTotal);
        Assert.Equal(0m, invoice.Total);
    }

    [Theory]
    [InlineData(1, 10, 0, 10, 0, 10)]
    [InlineData(3, 33.33, 23, 99.99, 23.00, 122.99)]
    public void Line_math_matches_expected(
        decimal qty,
        decimal unitPrice,
        decimal vatRate,
        decimal expectedSubtotal,
        decimal expectedVat,
        decimal expectedTotal)
    {
        var item = new InvoiceItem
        {
            Quantity = qty,
            UnitPrice = unitPrice,
            VatRate = vatRate,
        };

        Assert.Equal(expectedSubtotal, item.Subtotal);
        Assert.Equal(expectedVat, item.VatAmount);
        Assert.Equal(expectedTotal, item.Total);
    }
}
