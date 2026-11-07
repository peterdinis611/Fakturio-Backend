using System.Globalization;
using Fakturio.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices.Pdf;

internal static class InvoicePdfShared
{
    public static readonly CultureInfo CultureSk = CultureInfo.GetCultureInfo("sk-SK");

    public static string Money(decimal value, string currency) =>
        $"{value.ToString("N2", CultureSk)} {currency}";

    public static string FormatIban(string iban)
    {
        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return string.Join(' ', Enumerable.Range(0, (clean.Length + 3) / 4)
            .Select(i => clean.Substring(i * 4, Math.Min(4, clean.Length - i * 4))));
    }

    public static string StatusLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Koncept",
        InvoiceStatus.Issued => "Vystavená",
        InvoiceStatus.Paid => "Zaplatená",
        InvoiceStatus.Cancelled => "Zrušená",
        InvoiceStatus.Overdue => "Po splatnosti",
        _ => status.ToString()
    };

    public static string DocumentTitle(InvoiceDocumentType type) => type switch
    {
        InvoiceDocumentType.Proforma => "Zálohová faktúra",
        InvoiceDocumentType.CreditNote => "Dobropis",
        InvoiceDocumentType.Quote => "Cenová ponuka",
        InvoiceDocumentType.DeliveryNote => "Dodací list",
        InvoiceDocumentType.SalesOrder => "Objednávka",
        _ => "Faktúra"
    };

    public static string PaymentMethodLabel(string? method) => method switch
    {
        "bank_transfer" => "Bankový prevod",
        "cash" => "Hotovosť",
        "card" => "Karta",
        "other" => "Iné",
        _ => string.IsNullOrWhiteSpace(method) ? "" : method,
    };

    public static void AddressBlock(
        IContainer container,
        string? street,
        string? city,
        string? postal,
        string? country)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(street))
                col.Item().Text(street!).FontSize(9);
            var cityLine = $"{postal} {city}".Trim();
            if (!string.IsNullOrWhiteSpace(cityLine))
                col.Item().Text(cityLine).FontSize(9);
            if (!string.IsNullOrWhiteSpace(country) && country is not "SK")
                col.Item().Text(country!).FontSize(9);
        });
    }

    public static void IdLines(
        IContainer container,
        string? ico,
        string? dic,
        string? icDph,
        bool? vatPayerHint)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(ico))
                col.Item().Text($"IČO: {ico}").FontSize(9);
            if (!string.IsNullOrWhiteSpace(dic))
                col.Item().Text($"DIČ: {dic}").FontSize(9);
            if (vatPayerHint == true && !string.IsNullOrWhiteSpace(icDph))
                col.Item().Text($"IČ DPH: {icDph}").FontSize(9);
            else if (vatPayerHint == false)
                col.Item().Text("Nie sme platiteľmi DPH").FontSize(8).FontColor("#5C6570");
            else if (!string.IsNullOrWhiteSpace(icDph))
                col.Item().Text($"IČ DPH: {icDph}").FontSize(9);
        });
    }

    public static void ContactLines(
        IContainer container,
        string? email,
        string? phone,
        string? website)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(email))
                col.Item().Text(email!).FontSize(8).FontColor("#5C6570");
            if (!string.IsNullOrWhiteSpace(phone))
                col.Item().Text(phone!).FontSize(8).FontColor("#5C6570");
            if (!string.IsNullOrWhiteSpace(website))
                col.Item().Text(website!).FontSize(8).FontColor("#5C6570");
        });
    }

    public static void DateRow(ColumnDescriptor col, string label, DateOnly date, string muted)
    {
        col.Item().PaddingBottom(3).Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontSize(9).FontColor(muted);
            r.ConstantItem(8);
            r.ConstantItem(72).AlignRight().Text(date.ToString("dd.MM.yyyy"))
                .FontSize(9).SemiBold();
        });
    }

    public static void TotalsRow(ColumnDescriptor col, string label, string value, string muted)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontSize(9).FontColor(muted);
            r.ConstantItem(90).AlignRight().Text(value).FontSize(9).SemiBold();
        });
    }

    public static void LegalBlock(IContainer container, IReadOnlyList<string> clauses, string ink, string muted)
    {
        container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .Padding(8)
            .Column(col =>
            {
                col.Item().Text("PRÁVNE ÚDAJE")
                    .FontSize(7.5f).SemiBold().FontColor(muted).LetterSpacing(0.04f);
                foreach (var clause in clauses)
                    col.Item().PaddingTop(3).Text(clause).FontSize(8).FontColor(ink);
            });
    }

    public static void Logo(IContainer container, byte[]? logoBytes, float maxHeight = 48)
    {
        if (logoBytes is null || logoBytes.Length == 0)
            return;
        container.Height(maxHeight).Image(logoBytes).FitArea();
    }

    public static void ItemsTable(
        IContainer container,
        Invoice invoice,
        string headerBg,
        string muted,
        bool zebra = false)
    {
        var showVat = invoice.SellerIsVatPayer;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3.2f);
                columns.ConstantColumn(48);
                columns.ConstantColumn(52);
                columns.RelativeColumn(1.2f);
                if (showVat) columns.ConstantColumn(44);
                columns.RelativeColumn(1.2f);
            });

            table.Header(header =>
            {
                header.Cell().Element(c => TableHeader(c, headerBg, muted)).Text("NÁZOV A POPIS");
                header.Cell().Element(c => TableHeader(c, headerBg, muted)).AlignRight().Text("POČET");
                header.Cell().Element(c => TableHeader(c, headerBg, muted)).AlignRight().Text("MJ");
                header.Cell().Element(c => TableHeader(c, headerBg, muted)).AlignRight().Text("JEDN. CENA");
                if (showVat)
                    header.Cell().Element(c => TableHeader(c, headerBg, muted)).AlignRight().Text("DPH %");
                header.Cell().Element(c => TableHeader(c, headerBg, muted)).AlignRight().Text("CELKOM");
            });

            var i = 0;
            foreach (var item in invoice.Items.OrderBy(x => x.SortOrder))
            {
                var stripe = zebra && i % 2 == 1;
                i++;
                table.Cell().Element(c => TableBody(c, stripe)).Text(item.Description);
                table.Cell().Element(c => TableBody(c, stripe)).AlignRight()
                    .Text(item.Quantity.ToString("0.####"));
                table.Cell().Element(c => TableBody(c, stripe)).AlignRight().Text(item.Unit);
                table.Cell().Element(c => TableBody(c, stripe)).AlignRight()
                    .Text(Money(item.UnitPrice, invoice.Currency));
                if (showVat)
                    table.Cell().Element(c => TableBody(c, stripe)).AlignRight()
                        .Text($"{item.VatRate:0.##}");
                table.Cell().Element(c => TableBody(c, stripe)).AlignRight()
                    .Text(Money(item.Total, invoice.Currency));
            }
        });
    }

    private static IContainer TableHeader(IContainer container, string headerBg, string muted) =>
        container.DefaultTextStyle(x => x.FontSize(7.5f).SemiBold().FontColor(muted))
            .Background(headerBg)
            .PaddingVertical(6)
            .PaddingHorizontal(5);

    private static IContainer TableBody(IContainer container, bool stripe)
    {
        var cell = stripe ? container.Background(Colors.Grey.Lighten4) : container;
        return cell
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(7).PaddingHorizontal(5)
            .DefaultTextStyle(x => x.FontSize(9));
    }

    public static void TotalsBlock(
        ColumnDescriptor right,
        Invoice invoice,
        string accent,
        string muted)
    {
        var showVat = invoice.SellerIsVatPayer;
        if (showVat && !InvoiceLegalTexts.IsReverseCharge(invoice))
        {
            TotalsRow(right, "Základ", Money(invoice.Subtotal, invoice.Currency), muted);
            TotalsRow(right, "DPH", Money(invoice.VatTotal, invoice.Currency), muted);
        }
        else if (InvoiceLegalTexts.IsReverseCharge(invoice))
        {
            TotalsRow(right, "Základ", Money(invoice.Subtotal, invoice.Currency), muted);
            TotalsRow(right, "DPH (prenesená)", Money(0m, invoice.Currency), muted);
        }

        right.Item().PaddingTop(4).AlignRight().Text("Celková suma")
            .FontSize(9).FontColor(muted);
        right.Item().AlignRight().Text(Money(invoice.Total, invoice.Currency))
            .FontSize(16).Bold().FontColor(accent);
    }

    public static void QrBlock(ColumnDescriptor left, InvoicePdfModel model)
    {
        if (model.Qr is null)
            return;

        left.Item().PaddingTop(10).Row(qrRow =>
        {
            qrRow.ConstantItem(108).Column(q =>
            {
                q.Item().Width(100).Height(100).Image(model.Qr.Png);
            });
            qrRow.RelativeItem().PaddingLeft(8).AlignMiddle().Column(lbl =>
            {
                lbl.Item().Text(model.Qr.Label).SemiBold().FontSize(10);
                lbl.Item().PaddingTop(2)
                    .Text("Naskenuj v bankovej aplikácii")
                    .FontSize(8).FontColor("#5C6570");
            });
        });
    }

    public static void CancelBanner(ColumnDescriptor col, Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Cancelled || string.IsNullOrWhiteSpace(invoice.CancelReason))
            return;

        col.Item().PaddingTop(8).Background(Colors.Red.Lighten4).Padding(8)
            .Text($"Zrušená: {invoice.CancelReason} ({invoice.CancelledAt:dd.MM.yyyy})");
    }
}
