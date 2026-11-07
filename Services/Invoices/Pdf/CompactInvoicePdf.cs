using Fakturio.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices.Pdf;

/// <summary>Dense ledger style — thin rules, tight spacing, ink-forward typography.</summary>
internal static class CompactInvoicePdf
{
    private const string Ink = "#111111";
    private const string Muted = "#666666";
    private const string Rule = "#CCCCCC";
    private const string Accent = "#111111";

    public static void Compose(IContainer container, InvoicePdfModel model)
    {
        var invoice = model.Invoice;
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Row(brand =>
                    {
                        if (model.LogoBytes is not null)
                            brand.ConstantItem(72).Height(28).Image(model.LogoBytes).FitArea();
                        brand.RelativeItem().PaddingLeft(model.LogoBytes is null ? 0 : 8).AlignMiddle()
                            .Text(invoice.SellerName ?? "—")
                            .FontSize(11).SemiBold().FontColor(Ink);
                    });
                    left.Item().PaddingTop(4).Text(CompactSellerMeta(invoice, model))
                        .FontSize(7.5f).FontColor(Muted);
                });

                row.ConstantItem(200).AlignRight().Column(right =>
                {
                    right.Item().Text($"{InvoicePdfShared.DocumentTitle(invoice.DocumentType)} {invoice.Number}")
                        .FontSize(12).Bold().FontColor(Ink);
                    right.Item().PaddingTop(2)
                        .Text(InvoicePdfShared.StatusLabel(invoice.Status).ToUpperInvariant())
                        .FontSize(7.5f).FontColor(Muted).LetterSpacing(0.06f);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Ink);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(buyer =>
                {
                    buyer.Item().Text("Odberateľ").FontSize(7).FontColor(Muted);
                    buyer.Item().Text(invoice.CustomerName).FontSize(10).SemiBold();
                    buyer.Item().PaddingTop(2).Element(c => InvoicePdfShared.AddressBlock(c,
                        invoice.CustomerStreet, invoice.CustomerCity,
                        invoice.CustomerPostalCode, invoice.CustomerCountry));
                    buyer.Item().PaddingTop(3).Element(c => InvoicePdfShared.IdLines(c,
                        invoice.CustomerIco, invoice.CustomerDic, invoice.CustomerIcDph, null));
                });

                row.ConstantItem(170).Column(meta =>
                {
                    CompactMeta(meta, "Vystavenie", invoice.IssueDate.ToString("dd.MM.yyyy"));
                    CompactMeta(meta, "Dodanie", (invoice.DeliveryDate ?? invoice.IssueDate).ToString("dd.MM.yyyy"));
                    CompactMeta(meta, "Splatnosť", invoice.DueDate.ToString("dd.MM.yyyy"));
                    if (!string.IsNullOrWhiteSpace(invoice.VariableSymbol))
                        CompactMeta(meta, "VS", invoice.VariableSymbol!);
                    if (!string.IsNullOrWhiteSpace(invoice.BankIban))
                        CompactMeta(meta, "IBAN", InvoicePdfShared.FormatIban(invoice.BankIban!));
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Rule);

            col.Item().PaddingTop(6).Element(c =>
                InvoicePdfShared.ItemsTable(c, invoice, Colors.Transparent, Muted));

            col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Rule);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                        left.Item().Text(invoice.Notes.Trim()).FontSize(8).FontColor(Muted);

                    if (model.Qr is not null)
                    {
                        left.Item().PaddingTop(8).Row(qr =>
                        {
                            qr.ConstantItem(72).Height(72).Image(model.Qr.Png);
                            qr.RelativeItem().PaddingLeft(6).AlignMiddle()
                                .Text(model.Qr.Label).FontSize(8).FontColor(Muted);
                        });
                    }
                });

                row.ConstantItem(180).Column(right =>
                {
                    InvoicePdfShared.TotalsBlock(right, invoice, Accent, Muted);
                });
            });

            if (model.LegalClauses.Count > 0)
            {
                col.Item().PaddingTop(10).Column(legal =>
                {
                    foreach (var clause in model.LegalClauses)
                        legal.Item().PaddingBottom(2).Text(clause).FontSize(7).FontColor(Muted);
                });
            }

            if (!string.IsNullOrWhiteSpace(model.FooterNote))
            {
                col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Rule);
                col.Item().PaddingTop(4).Text(model.FooterNote).FontSize(7).FontColor(Muted);
            }

            InvoicePdfShared.CancelBanner(col, invoice);
        });
    }

    private static void CompactMeta(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(2).Row(r =>
        {
            r.ConstantItem(56).Text(label).FontSize(7).FontColor(Muted);
            r.RelativeItem().AlignRight().Text(value).FontSize(8).SemiBold().FontColor(Ink);
        });
    }

    private static string CompactSellerMeta(Invoice invoice, InvoicePdfModel model)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(invoice.SellerIco)) bits.Add($"IČO {invoice.SellerIco}");
        if (!string.IsNullOrWhiteSpace(invoice.SellerDic)) bits.Add($"DIČ {invoice.SellerDic}");
        if (invoice.SellerIsVatPayer && !string.IsNullOrWhiteSpace(invoice.SellerIcDph))
            bits.Add($"IČ DPH {invoice.SellerIcDph}");
        if (!string.IsNullOrWhiteSpace(model.SellerEmail)) bits.Add(model.SellerEmail!);
        return string.Join("  ·  ", bits);
    }
}
