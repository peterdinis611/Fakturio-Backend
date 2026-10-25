using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class InvoiceMapper
{
    public static InvoiceListItemResponse ToListItem(Invoice i) => new(
        i.Id,
        i.Number,
        i.Status,
        i.CustomerId,
        i.CustomerName,
        i.IssueDate,
        i.DueDate,
        i.Currency,
        i.Total,
        i.CreatedAt
    );

    public static InvoiceResponse ToResponse(Invoice i) => new(
        i.Id,
        i.Number,
        i.Status,
        i.CustomerId,
        new InvoicePartyResponse(
            i.CustomerName,
            i.CustomerIco,
            i.CustomerDic,
            i.CustomerIcDph,
            i.CustomerEmail,
            i.CustomerStreet,
            i.CustomerCity,
            i.CustomerPostalCode,
            i.CustomerCountry
        ),
        string.IsNullOrWhiteSpace(i.SellerName)
            ? null
            : new InvoicePartyResponse(
                i.SellerName,
                i.SellerIco,
                i.SellerDic,
                i.SellerIcDph,
                null,
                i.SellerStreet,
                i.SellerCity,
                i.SellerPostalCode,
                i.SellerCountry
            ),
        i.IssueDate,
        i.DueDate,
        i.DeliveryDate,
        i.PaidAt,
        i.Currency,
        i.VariableSymbol,
        i.ConstantSymbol,
        i.SpecificSymbol,
        i.PaymentMethod,
        i.BankIban,
        i.BankBic,
        i.Notes,
        i.Subtotal,
        i.VatTotal,
        i.Total,
        i.CreatedAt,
        i.UpdatedAt,
        i.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new InvoiceItemResponse(
                item.Id,
                item.Description,
                item.Unit,
                item.Quantity,
                item.UnitPrice,
                item.VatRate,
                item.Subtotal,
                item.VatAmount,
                item.Total,
                item.SortOrder
            ))
            .ToList()
    );

    public static void ApplyCustomerSnapshot(Invoice invoice, Customer customer)
    {
        invoice.CustomerId = customer.Id;
        invoice.CustomerName = customer.Name;
        invoice.CustomerIco = customer.Ico;
        invoice.CustomerDic = customer.Dic;
        invoice.CustomerIcDph = customer.IcDph;
        invoice.CustomerEmail = customer.Email;
        invoice.CustomerStreet = customer.Street;
        invoice.CustomerCity = customer.City;
        invoice.CustomerPostalCode = customer.PostalCode;
        invoice.CustomerCountry = customer.Country;
    }

    public static void ApplySellerSnapshot(Invoice invoice, CompanySettings? company)
    {
        if (company is null || string.IsNullOrWhiteSpace(company.Name))
            return;

        invoice.SellerName = company.Name;
        invoice.SellerIco = company.Ico;
        invoice.SellerDic = company.Dic;
        invoice.SellerIcDph = company.IcDph;
        invoice.SellerStreet = company.Street;
        invoice.SellerCity = company.City;
        invoice.SellerPostalCode = company.PostalCode;
        invoice.SellerCountry = company.Country;
    }

    public static void ReplaceItems(
        Invoice invoice,
        IReadOnlyList<CreateInvoiceItemRequest> items,
        decimal defaultVatRate)
    {
        invoice.Items.Clear();
        var order = 0;
        foreach (var item in items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description.Trim(),
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "ks" : item.Unit.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = item.VatRate ?? defaultVatRate,
                SortOrder = order++
            });
        }
    }
}
