using Fakturio.Api.Dtos;
using Fakturio.Api.Models;

namespace Fakturio.Api.Mapping;

public static class InvoiceMapper
{
    public static InvoiceListItemResponse ToListItem(Invoice i, DateOnly today) => new(
        i.Id,
        i.Number,
        i.Status,
        i.CustomerId,
        i.CustomerName,
        i.IssueDate,
        i.DueDate,
        i.PaidAt,
        i.Currency,
        i.Total,
        IsOverdue(i, today),
        DaysUntilDue(i, today),
        i.CreatedAt
    );

    public static InvoiceResponse ToResponse(Invoice i, DateOnly? today = null)
    {
        today ??= DateOnly.FromDateTime(DateTime.UtcNow);
        return new(
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
            i.CancelReason,
            i.CancelledAt,
            i.LastReminderSentAt,
            i.ReminderCount,
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
            IsOverdue(i, today.Value),
            DaysUntilDue(i, today.Value),
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
    }

    public static InvoiceReminderItemResponse ToReminderItem(Invoice i, DateOnly today) => new(
        i.Id,
        i.Number,
        i.Status,
        i.CustomerId,
        i.CustomerName,
        i.CustomerEmail,
        i.IssueDate,
        i.DueDate,
        i.Currency,
        i.Total,
        IsOverdue(i, today),
        today.DayNumber - i.DueDate.DayNumber,
        i.ReminderCount,
        i.LastReminderSentAt
    );

    public static InvoiceStatusResponse ToStatusResponse(Invoice i) => new(
        i.Id,
        i.Number,
        i.Status,
        i.PaidAt,
        i.CancelReason,
        i.CancelledAt
    );

    public static bool IsOverdue(Invoice i, DateOnly today) =>
        i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue
        && i.DueDate < today
        && i.PaidAt is null;

    public static int? DaysUntilDue(Invoice i, DateOnly today)
    {
        if (i.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled or InvoiceStatus.Draft)
            return null;

        return i.DueDate.DayNumber - today.DayNumber;
    }

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
