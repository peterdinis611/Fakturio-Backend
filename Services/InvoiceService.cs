using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services;

public class InvoiceService(AppDbContext db, InvoiceNumberService numbers)
{
    public async Task<PagedResponse<InvoiceListItemResponse>> ListAsync(
        int page,
        int pageSize,
        InvoiceStatus? status,
        Guid? customerId,
        string? search,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Invoices.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.Number.Contains(term) ||
                i.CustomerName.Contains(term) ||
                (i.VariableSymbol != null && i.VariableSymbol.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<InvoiceListItemResponse>(
            items.Select(InvoiceMapper.ToListItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<InvoiceResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return invoice is null ? null : InvoiceMapper.ToResponse(invoice);
    }

    public async Task<(InvoiceResponse? Invoice, string? Error, int StatusCode)> CreateAsync(
        CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([request.CustomerId], ct);
        if (customer is null)
            return (null, "Customer not found.", StatusCodes.Status400BadRequest);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = company?.DefaultDueDays ?? 14;
        var dueDate = request.DueDate ?? issueDate.AddDays(dueDays);
        var defaultVat = company?.DefaultVatRate ?? 23m;
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? company?.DefaultCurrency ?? "EUR"
            : request.Currency.Trim().ToUpperInvariant();

        var status = request.Status ?? InvoiceStatus.Issued;
        var number = await numbers.NextAsync(company?.InvoicePrefix, ct);

        var invoice = new Invoice
        {
            Number = number,
            Status = status,
            IssueDate = issueDate,
            DueDate = dueDate,
            DeliveryDate = request.DeliveryDate,
            Currency = currency,
            VariableSymbol = request.VariableSymbol ?? DigitsOnly(number),
            ConstantSymbol = request.ConstantSymbol,
            SpecificSymbol = request.SpecificSymbol,
            PaymentMethod = request.PaymentMethod ?? "bank_transfer",
            BankIban = NormalizeIban(request.BankIban) ?? company?.BankIban,
            BankBic = request.BankBic?.Trim().ToUpperInvariant() ?? company?.BankBic,
            Notes = request.Notes?.Trim() ?? company?.Notes,
            PaidAt = status == InvoiceStatus.Paid ? issueDate : null
        };

        InvoiceMapper.ApplyCustomerSnapshot(invoice, customer);
        InvoiceMapper.ApplySellerSnapshot(invoice, company);
        InvoiceMapper.ReplaceItems(invoice, request.Items, defaultVat);
        InvoiceCalculator.RecalculateTotals(invoice);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return (InvoiceMapper.ToResponse(invoice), null, StatusCodes.Status201Created);
    }

    public async Task<(InvoiceResponse? Invoice, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
            return (null, "Invoice not found.", StatusCodes.Status404NotFound);

        if (invoice.Status is not InvoiceStatus.Draft)
            return (null, "Only draft invoices can be fully updated.", StatusCodes.Status409Conflict);

        var customer = await db.Customers.FindAsync([request.CustomerId], ct);
        if (customer is null)
            return (null, "Customer not found.", StatusCodes.Status400BadRequest);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);

        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.DeliveryDate = request.DeliveryDate;
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? invoice.Currency
            : request.Currency.Trim().ToUpperInvariant();
        invoice.VariableSymbol = request.VariableSymbol ?? invoice.VariableSymbol;
        invoice.ConstantSymbol = request.ConstantSymbol;
        invoice.SpecificSymbol = request.SpecificSymbol;
        invoice.PaymentMethod = request.PaymentMethod ?? invoice.PaymentMethod;
        invoice.BankIban = NormalizeIban(request.BankIban) ?? invoice.BankIban;
        invoice.BankBic = request.BankBic?.Trim().ToUpperInvariant() ?? invoice.BankBic;
        invoice.Notes = request.Notes?.Trim();

        InvoiceMapper.ApplyCustomerSnapshot(invoice, customer);
        InvoiceMapper.ReplaceItems(invoice, request.Items, company?.DefaultVatRate ?? 23m);
        InvoiceCalculator.RecalculateTotals(invoice);

        await db.SaveChangesAsync(ct);
        return (InvoiceMapper.ToResponse(invoice), null, StatusCodes.Status200OK);
    }

    public async Task<(InvoiceStatusResponse? Result, string? Error, int StatusCode)> UpdateStatusAsync(
        Guid id,
        UpdateInvoiceStatusRequest request,
        CancellationToken ct)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return (null, "Invoice not found.", StatusCodes.Status404NotFound);

        if (invoice.Status == InvoiceStatus.Cancelled && request.Status != InvoiceStatus.Cancelled)
            return (null, "Cancelled invoices cannot change status.", StatusCodes.Status409Conflict);

        if (invoice.Status == InvoiceStatus.Paid && request.Status is InvoiceStatus.Draft)
            return (null, "Paid invoices cannot return to draft.", StatusCodes.Status409Conflict);

        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (request.Status == InvoiceStatus.Paid)
            invoice.PaidAt ??= DateOnly.FromDateTime(DateTime.UtcNow);
        else if (request.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await db.SaveChangesAsync(ct);

        return (new InvoiceStatusResponse(invoice.Id, invoice.Number, invoice.Status, invoice.PaidAt),
            null, StatusCodes.Status200OK);
    }

    public async Task<(InvoiceResponse? Invoice, string? Error, int StatusCode)> DuplicateAsync(
        Guid id,
        CancellationToken ct)
    {
        var source = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (source is null)
            return (null, "Invoice not found.", StatusCodes.Status404NotFound);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = company?.DefaultDueDays ?? 14;
        var number = await numbers.NextAsync(company?.InvoicePrefix, ct);

        var clone = new Invoice
        {
            Number = number,
            Status = InvoiceStatus.Draft,
            CustomerId = source.CustomerId,
            CustomerName = source.CustomerName,
            CustomerIco = source.CustomerIco,
            CustomerDic = source.CustomerDic,
            CustomerIcDph = source.CustomerIcDph,
            CustomerEmail = source.CustomerEmail,
            CustomerStreet = source.CustomerStreet,
            CustomerCity = source.CustomerCity,
            CustomerPostalCode = source.CustomerPostalCode,
            CustomerCountry = source.CustomerCountry,
            SellerName = source.SellerName,
            SellerIco = source.SellerIco,
            SellerDic = source.SellerDic,
            SellerIcDph = source.SellerIcDph,
            SellerStreet = source.SellerStreet,
            SellerCity = source.SellerCity,
            SellerPostalCode = source.SellerPostalCode,
            SellerCountry = source.SellerCountry,
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(dueDays),
            DeliveryDate = null,
            Currency = source.Currency,
            VariableSymbol = DigitsOnly(number),
            ConstantSymbol = source.ConstantSymbol,
            SpecificSymbol = source.SpecificSymbol,
            PaymentMethod = source.PaymentMethod,
            BankIban = source.BankIban,
            BankBic = source.BankBic,
            Notes = source.Notes
        };

        foreach (var item in source.Items.OrderBy(i => i.SortOrder))
        {
            clone.Items.Add(new InvoiceItem
            {
                Description = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = item.VatRate,
                SortOrder = item.SortOrder
            });
        }

        InvoiceCalculator.RecalculateTotals(clone);
        db.Invoices.Add(clone);
        await db.SaveChangesAsync(ct);

        return (InvoiceMapper.ToResponse(clone), null, StatusCodes.Status201Created);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return (false, "Invoice not found.", StatusCodes.Status404NotFound);

        if (invoice.Status is not InvoiceStatus.Draft)
            return (false, "Only draft invoices can be deleted. Cancel issued invoices instead.",
                StatusCodes.Status409Conflict);

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return (true, null, StatusCodes.Status204NoContent);
    }

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string? NormalizeIban(string? iban) =>
        string.IsNullOrWhiteSpace(iban)
            ? null
            : iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}
