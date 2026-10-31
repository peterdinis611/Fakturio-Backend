using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Invoices;

namespace Fakturio.Api.Services.Exports;

/// <summary>
/// Money S3–oriented issued-invoice XML (SeznamFaktVyd) that accountants commonly import.
/// </summary>
public static class MoneyS3XmlExport
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static byte[] Build(IReadOnlyList<Invoice> invoices, string? sellerIco)
    {
        var list = new XElement("SeznamFaktVyd");
        foreach (var invoice in invoices.OrderBy(i => i.IssueDate).ThenBy(i => i.Number))
        {
            list.Add(BuildInvoice(invoice));
        }

        var root = new XElement(
            "MoneyData",
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute("version", "1.0"),
            new XAttribute("aplikace", "Fakturio"),
            new XAttribute("icoFirmy", sellerIco ?? ""),
            list);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        {
            doc.Save(writer);
        }

        return ms.ToArray();
    }

    private static XElement BuildInvoice(Invoice invoice)
    {
        var typ = invoice.DocumentType switch
        {
            InvoiceDocumentType.Proforma => "2",
            InvoiceDocumentType.CreditNote => "3",
            _ => "1",
        };

        var polozky = new XElement("SeznamPolozek");
        foreach (var item in invoice.Items.OrderBy(i => i.SortOrder))
        {
            polozky.Add(new XElement(
                "Polozka",
                El("Popis", item.Description),
                El("PocetMJ", Dec(item.Quantity)),
                El("MJ", item.Unit),
                El("Cena", Dec(item.UnitPrice)),
                El("SazbaDPH", Dec(item.VatRate)),
                El("Zaklad", Dec(item.Subtotal)),
                El("DPH", Dec(item.VatAmount)),
                El("Celkem", Dec(item.Total))));
        }

        return new XElement(
            "FaktVyd",
            El("Doklad", invoice.Number),
            El("TypDokladu", typ),
            El("DatVyst", InvoiceLegalTexts.FormatMoneyS3Date(invoice.IssueDate)),
            El("DatPln", InvoiceLegalTexts.FormatMoneyS3Date(invoice.DeliveryDate ?? invoice.IssueDate)),
            El("DatSplat", InvoiceLegalTexts.FormatMoneyS3Date(invoice.DueDate)),
            El("DatUhrady", invoice.PaidAt is { } paid
                ? InvoiceLegalTexts.FormatMoneyS3Date(paid)
                : ""),
            El("VarSym", invoice.VariableSymbol ?? ""),
            El("KonstSym", invoice.ConstantSymbol ?? ""),
            El("SpecSym", invoice.SpecificSymbol ?? ""),
            El("KodMeny", invoice.Currency),
            El("Stav", StatusCode(invoice.Status)),
            El("RezimDPH", InvoiceLegalTexts.VatRegimeLabel(invoice)),
            El("PreneseniDane", InvoiceLegalTexts.IsReverseCharge(invoice) ? "1" : "0"),
            new XElement(
                "Dodavatel",
                El("Nazev", invoice.SellerName ?? ""),
                El("ICO", invoice.SellerIco ?? ""),
                El("DIC", invoice.SellerDic ?? ""),
                El("ICDPH", invoice.SellerIcDph ?? ""),
                El("Ulice", invoice.SellerStreet ?? ""),
                El("Mesto", invoice.SellerCity ?? ""),
                El("PSC", invoice.SellerPostalCode ?? ""),
                El("Stat", invoice.SellerCountry ?? "SK"),
                El("PlatceDPH", invoice.SellerIsVatPayer ? "1" : "0")),
            new XElement(
                "Odberatel",
                El("Nazev", invoice.CustomerName),
                El("ICO", invoice.CustomerIco ?? ""),
                El("DIC", invoice.CustomerDic ?? ""),
                El("ICDPH", invoice.CustomerIcDph ?? ""),
                El("Ulice", invoice.CustomerStreet ?? ""),
                El("Mesto", invoice.CustomerCity ?? ""),
                El("PSC", invoice.CustomerPostalCode ?? ""),
                El("Stat", invoice.CustomerCountry)),
            El("Zaklad", Dec(invoice.Subtotal)),
            El("DPH", Dec(invoice.VatTotal)),
            El("Celkem", Dec(invoice.Total)),
            El("Poznamka", invoice.Notes ?? ""),
            El("CisloObj", invoice.RelatedInvoiceNumber ?? ""),
            El("IBAN", invoice.BankIban ?? ""),
            El("BIC", invoice.BankBic ?? ""),
            polozky);
    }

    private static XElement El(string name, string value) => new(name, value ?? "");

    private static string Dec(decimal value) => value.ToString("0.00", Inv);

    private static string StatusCode(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "koncept",
        InvoiceStatus.Issued => "vystavena",
        InvoiceStatus.Paid => "uhrazena",
        InvoiceStatus.Overdue => "po_splatnosti",
        InvoiceStatus.Cancelled => "zrusena",
        _ => status.ToString().ToLowerInvariant(),
    };
}
