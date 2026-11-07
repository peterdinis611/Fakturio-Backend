using Fakturio.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices.Pdf;

/// <summary>Bold header band, strong hierarchy, QR + totals side-by-side — no mid-page bank clutter.</summary>
internal static class ModernInvoicePdf
{
    private const string Accent = "#0B6E4F";
    private const string AccentInk = "#FFFFFF";
    private const string Surface = "#F4F7F5";
    private const string Ink = "#12151A";
    private const string Muted = "#5A6570";

    public static void Compose(IContainer container, InvoicePdfModel model)
    {
        var invoice = model.Invoice;
        container.Column(col =>
        {
            col.Item().Background(Accent).Padding(14).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (model.LogoBytes is not null)
                    {
                        left.Item().Height(36).Width(120).Image(model.LogoBytes).FitArea();
                        left.Item().PaddingTop(8);
                    }
                    left.Item().Text(invoice.SellerName ?? "—")
                        .FontSize(14).SemiBold().FontColor(AccentInk);
                    left.Item().PaddingTop(2).Text(BuildSellerOneLiner(invoice, model))
                        .FontSize(8).FontColor("#D7EDE4");
                });

                row.ConstantItem(210).AlignRight().Column(right =>
                {
                    right.Item().Text(InvoicePdfShared.DocumentTitle(invoice.DocumentType).ToUpperInvariant())
                        .FontSize(9).FontColor("#B8DDCF").LetterSpacing(0.08f);
                    right.Item().Text(invoice.Number)
                        .FontSize(20).Bold().FontColor(AccentInk);
                    right.Item().PaddingTop(4).Text(InvoicePdfShared.StatusLabel(invoice.Status))
                        .FontSize(9).FontColor("#D7EDE4");
                });
            });

            col.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Background(Surface).Padding(10).Column(buyer =>
                {
                    buyer.Item().Text("ODBERATEĽ")
                        .FontSize(7.5f).FontColor(Muted).LetterSpacing(0.05f);
                    buyer.Item().PaddingTop(3).Text(invoice.CustomerName)
                        .FontSize(12).SemiBold().FontColor(Ink);
                    buyer.Item().PaddingTop(4).Element(c => InvoicePdfShared.AddressBlock(c,
                        invoice.CustomerStreet, invoice.CustomerCity,
                        invoice.CustomerPostalCode, invoice.CustomerCountry));
                    buyer.Item().PaddingTop(6).Element(c => InvoicePdfShared.IdLines(c,
                        invoice.CustomerIco, invoice.CustomerDic, invoice.CustomerIcDph, null));
                });

                row.ConstantItem(12);
                row.ConstantItem(190).Padding(4).Column(dates =>
                {
                    Meta(dates, "Vystavenie", invoice.IssueDate.ToString("dd.MM.yyyy"));
                    Meta(dates, "Dodanie", (invoice.DeliveryDate ?? invoice.IssueDate).ToString("dd.MM.yyyy"));
                    Meta(dates, "Splatnosť", invoice.DueDate.ToString("dd.MM.yyyy"));
                    if (invoice.PaidAt.HasValue)
                        Meta(dates, "Zaplatené", invoice.PaidAt.Value.ToString("dd.MM.yyyy"));
                    var pay = InvoicePdfShared.PaymentMethodLabel(invoice.PaymentMethod);
                    if (!string.IsNullOrEmpty(pay))
                        Meta(dates, "Platba", pay);
                    if (!string.IsNullOrWhiteSpace(invoice.VariableSymbol))
                        Meta(dates, "VS", invoice.VariableSymbol!);
                });
            });

            col.Item().PaddingTop(14).Element(c =>
                InvoicePdfShared.ItemsTable(c, invoice, Surface, Muted, zebra: true));

            col.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        left.Item().Text("Poznámka").FontSize(8).FontColor(Muted);
                        left.Item().PaddingTop(2).Text(invoice.Notes.Trim()).FontSize(9);
                    }

                    InvoicePdfShared.QrBlock(left, model);

                    if (!string.IsNullOrWhiteSpace(invoice.BankIban))
                    {
                        left.Item().PaddingTop(12).Background(Surface).Padding(8).Column(bank =>
                        {
                            bank.Item().Text(model.BankName ?? "Platobné údaje")
                                .FontSize(8).SemiBold().FontColor(Muted);
                            bank.Item().PaddingTop(2)
                                .Text(InvoicePdfShared.FormatIban(invoice.BankIban!))
                                .FontSize(10).SemiBold();
                            if (!string.IsNullOrWhiteSpace(invoice.BankBic))
                                bank.Item().Text($"SWIFT {invoice.BankBic}").FontSize(8).FontColor(Muted);
                        });
                    }
                });

                row.ConstantItem(200).AlignRight().Column(right =>
                {
                    InvoicePdfShared.TotalsBlock(right, invoice, Accent, Muted);
                });
            });

            if (model.LegalClauses.Count > 0)
                col.Item().PaddingTop(12).Element(c =>
                    InvoicePdfShared.LegalBlock(c, model.LegalClauses, Ink, Muted));

            if (!string.IsNullOrWhiteSpace(model.FooterNote))
                col.Item().PaddingTop(8).Text(model.FooterNote).FontSize(8).FontColor(Muted);

            InvoicePdfShared.CancelBanner(col, invoice);
        });
    }

    private static void Meta(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(5).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8).FontColor(Muted);
            r.ConstantItem(100).AlignRight().Text(value).FontSize(9).SemiBold().FontColor(Ink);
        });
    }

    private static string BuildSellerOneLiner(Invoice invoice, InvoicePdfModel model)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(invoice.SellerIco))
            parts.Add($"IČO {invoice.SellerIco}");
        if (!string.IsNullOrWhiteSpace(model.SellerEmail))
            parts.Add(model.SellerEmail!);
        else if (!string.IsNullOrWhiteSpace(model.SellerPhone))
            parts.Add(model.SellerPhone!);
        var city = $"{invoice.SellerPostalCode} {invoice.SellerCity}".Trim();
        if (!string.IsNullOrWhiteSpace(city))
            parts.Add(city);
        return string.Join(" · ", parts);
    }
}
