using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Fakturio.Api.Models;

namespace Fakturio.Api.Services.Invoices;

/// <summary>
/// ISDOC 6.0.1 (http://isdoc.cz/namespace/2013) export for issued tax documents.
/// </summary>
public static class IsdocExport
{
    private static readonly XNamespace Ns = "http://isdoc.cz/namespace/2013";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Build(Invoice invoice)
    {
        var issueDate = invoice.IssueDate.ToString("yyyy-MM-dd", Inv);
        var taxPoint = (invoice.DeliveryDate ?? invoice.IssueDate).ToString("yyyy-MM-dd", Inv);
        var documentType = invoice.DocumentType switch
        {
            InvoiceDocumentType.CreditNote => 2,
            InvoiceDocumentType.Proforma => 4,
            InvoiceDocumentType.Invoice => 1,
            _ => 1
        };

        var vatGroups = invoice.Items
            .GroupBy(i => i.VatRate)
            .OrderBy(g => g.Key)
            .ToList();

        var lines = new XElement(Ns + "InvoiceLines");
        var lineNumber = 1;
        foreach (var item in invoice.Items.OrderBy(i => i.SortOrder))
        {
            lines.Add(new XElement(
                Ns + "InvoiceLine",
                El("ID", lineNumber.ToString(Inv)),
                new XElement(
                    Ns + "InvoicedQuantity",
                    new XAttribute("unitCode", MapUnitCode(item.Unit)),
                    Dec(item.Quantity)),
                El("LineExtensionAmount", Dec(item.Subtotal)),
                El("LineExtensionAmountTaxInclusive", Dec(item.Total)),
                El("LineExtensionTaxAmount", Dec(item.VatAmount)),
                El("UnitPrice", Dec(item.UnitPrice)),
                El("UnitPriceTaxInclusive", Dec(item.Quantity > 0 ? item.Total / item.Quantity : item.UnitPrice)),
                new XElement(
                    Ns + "ClassifiedTaxCategory",
                    El("Percent", Dec(item.VatRate)),
                    El("VATCalculationMethod", "0")),
                new XElement(
                    Ns + "Item",
                    El("Description", item.Description))));
            lineNumber++;
        }

        var taxSubTotals = new List<XElement>();
        foreach (var group in vatGroups)
        {
            var taxable = group.Sum(i => i.Subtotal);
            var tax = group.Sum(i => i.VatAmount);
            var inclusive = group.Sum(i => i.Total);
            taxSubTotals.Add(new XElement(
                Ns + "TaxSubTotal",
                El("TaxableAmount", Dec(taxable)),
                El("TaxAmount", Dec(tax)),
                El("TaxInclusiveAmount", Dec(inclusive)),
                El("Percent", Dec(group.Key))));
        }

        var taxTotal = new XElement(
            Ns + "TaxTotal",
            taxSubTotals,
            El("TaxAmount", Dec(invoice.VatTotal)));

        var monetary = new XElement(
            Ns + "LegalMonetaryTotal",
            El("TaxExclusiveAmount", Dec(invoice.Subtotal)),
            El("TaxInclusiveAmount", Dec(invoice.Total)),
            El("AlreadyClaimedTaxInclusiveAmount", Dec(0m)),
            El("PayableAmount", Dec(invoice.Total)));

        XElement? paymentMeans = null;
        if (!string.IsNullOrWhiteSpace(invoice.BankIban) || !string.IsNullOrWhiteSpace(invoice.VariableSymbol))
        {
            paymentMeans = new XElement(
                Ns + "PaymentMeans",
                new XElement(
                    Ns + "Payment",
                    El("PaidAmount", Dec(invoice.Total)),
                    El("PaymentMeansCode", "42"),
                    new XElement(
                        Ns + "Details",
                        El("PaymentDueDate", invoice.DueDate.ToString("yyyy-MM-dd", Inv)),
                        El("ID", invoice.BankIban ?? ""),
                        El("VariableSymbol", invoice.VariableSymbol ?? ""),
                        El("ConstantSymbol", invoice.ConstantSymbol ?? ""),
                        El("SpecificSymbol", invoice.SpecificSymbol ?? ""))));
        }

        var rootChildren = new List<object>
        {
            new XAttribute("version", "6.0.1"),
            El("DocumentType", documentType.ToString(Inv)),
            El("SubDocumentType", ""),
            El("SubDocumentTypeOrigin", "Fakturio"),
            El("ID", invoice.Number),
            El("UUID", invoice.Id.ToString("D")),
            El("IssuingSystem", "Fakturio"),
            El("IssueDate", issueDate),
            El("TaxPointDate", taxPoint),
            El("VATApplicable", invoice.SellerIsVatPayer ? "true" : "false"),
            new XElement(Ns + "ElectronicPossibilityAgreementReference")
        };

        var noteElement = OptionalNote(invoice.Notes);
        if (noteElement is not null)
            rootChildren.Add(noteElement);

        rootChildren.AddRange(
        [
            El("LocalCurrencyCode", invoice.Currency),
            El("CurrRate", Dec(1m)),
            El("RefCurrRate", Dec(1m)),
            BuildParty("AccountingSupplierParty", invoice.SellerName, invoice.SellerIco, invoice.SellerDic,
                invoice.SellerIcDph, invoice.SellerStreet, invoice.SellerCity, invoice.SellerPostalCode,
                invoice.SellerCountry),
            BuildParty("AccountingCustomerParty", invoice.CustomerName, invoice.CustomerIco, invoice.CustomerDic,
                invoice.CustomerIcDph, invoice.CustomerStreet, invoice.CustomerCity, invoice.CustomerPostalCode,
                invoice.CustomerCountry),
            lines,
            taxTotal,
            monetary
        ]);

        if (paymentMeans is not null)
            rootChildren.Add(paymentMeans);

        var root = new XElement(Ns + "Invoice", rootChildren);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            root);

        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
        {
            doc.Save(writer, SaveOptions.DisableFormatting);
        }

        return sb.ToString();
    }

    private static XElement BuildParty(
        string elementName,
        string? name,
        string? ico,
        string? dic,
        string? icDph,
        string? street,
        string? city,
        string? postalCode,
        string? country)
    {
        var party = new XElement(Ns + "Party");

        if (!string.IsNullOrWhiteSpace(ico))
            party.Add(new XElement(Ns + "PartyIdentification", El("ID", ico.Trim())));

        party.Add(new XElement(Ns + "PartyName", El("Name", name ?? "")));

        party.Add(new XElement(
            Ns + "PostalAddress",
            El("StreetName", street ?? ""),
            El("CityName", city ?? ""),
            El("PostalZone", postalCode ?? ""),
            new XElement(
                Ns + "Country",
                El("IdentificationCode", string.IsNullOrWhiteSpace(country) ? "SK" : country.Trim().ToUpperInvariant()))));

        var companyId = TaxCompanyId(icDph, dic, country);
        party.Add(new XElement(
            Ns + "PartyTaxScheme",
            El("CompanyID", companyId)));

        return new XElement(Ns + elementName, party);
    }

    private static string TaxCompanyId(string? icDph, string? dic, string? country)
    {
        if (!string.IsNullOrWhiteSpace(icDph))
            return icDph.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(dic))
            return "";

        var code = string.IsNullOrWhiteSpace(country) ? "SK" : country.Trim().ToUpperInvariant();
        return dic.StartsWith(code, StringComparison.OrdinalIgnoreCase)
            ? dic.Trim().ToUpperInvariant()
            : $"{code}{dic.Trim()}";
    }

    private static XElement? OptionalNote(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : new XElement(Ns + "Note", notes.Trim());

    private static string MapUnitCode(string unit)
    {
        var key = unit.Trim().ToLowerInvariant();
        return key switch
        {
            "hod" or "h" or "hod." => "HUR",
            "kg" => "KGM",
            "m" => "MTR",
            "m2" or "m²" => "MTK",
            "l" or "litr" => "LTR",
            _ => "C62"
        };
    }

    private static XElement El(string name, string value) =>
        new(Ns + name, value ?? "");

    private static string Dec(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", Inv);
}
