using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public class InvoiceService(AppDbContext db, IInvoiceNumberService numbers) : IInvoiceService
{
    public async Task<PagedResponse<InvoiceListItemResponse>> ListAsync(
        InvoiceListQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var invoices = db.Invoices.AsNoTracking().AsQueryable();

        if (query.Status.HasValue)
            invoices = invoices.Where(i => i.Status == query.Status.Value);

        if (query.CustomerId.HasValue)
            invoices = invoices.Where(i => i.CustomerId == query.CustomerId.Value);

        if (query.IssueFrom.HasValue)
            invoices = invoices.Where(i => i.IssueDate >= query.IssueFrom.Value);

        if (query.IssueTo.HasValue)
            invoices = invoices.Where(i => i.IssueDate <= query.IssueTo.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            invoices = invoices.Where(i =>
                i.Number.Contains(term) ||
                i.CustomerName.Contains(term) ||
                (i.VariableSymbol != null && i.VariableSymbol.Contains(term)));
        }

        var totalCount = await invoices.CountAsync(ct);
        var items = await invoices
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<InvoiceListItemResponse>(
            items.Select(InvoiceMapper.ToListItem).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return invoice is null ? null : InvoiceMapper.ToResponse(invoice);
    }

    public async Task<InvoiceResponse?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;

        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Number == number.Trim(), ct);

        return invoice is null ? null : InvoiceMapper.ToResponse(invoice);
    }

    public async Task<ServiceResult<InvoiceResponse>> CreateAsync(
        CreateInvoiceRequest request,
        CancellationToken ct = default)
    {
        var customer = await db.Customers.FindAsync([request.CustomerId], ct);
        if (customer is null)
            return ServiceResult<InvoiceResponse>.BadRequest("Customer not found.", "customer_not_found");

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

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceResponse>> UpdateAsync(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.");

        if (invoice.Status is not InvoiceStatus.Draft)
            return ServiceResult<InvoiceResponse>.Conflict(
                "Only draft invoices can be fully updated.",
                "not_draft");

        var customer = await db.Customers.FindAsync([request.CustomerId], ct);
        if (customer is null)
            return ServiceResult<InvoiceResponse>.BadRequest("Customer not found.", "customer_not_found");

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
        return ServiceResult<InvoiceResponse>.Ok(InvoiceMapper.ToResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceStatusResponse>> UpdateStatusAsync(
        Guid id,
        UpdateInvoiceStatusRequest request,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return ServiceResult<InvoiceStatusResponse>.NotFound("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Cancelled && request.Status != InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Cancelled invoices cannot change status.",
                "cancelled");

        if (invoice.Status == InvoiceStatus.Paid && request.Status is InvoiceStatus.Draft)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Paid invoices cannot return to draft.",
                "paid_to_draft");

        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (request.Status == InvoiceStatus.Paid)
            invoice.PaidAt ??= DateOnly.FromDateTime(DateTime.UtcNow);
        else if (request.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await db.SaveChangesAsync(ct);

        return ServiceResult<InvoiceStatusResponse>.Ok(
            new InvoiceStatusResponse(invoice.Id, invoice.Number, invoice.Status, invoice.PaidAt));
    }

    public async Task<ServiceResult<InvoiceResponse>> DuplicateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var source = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (source is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.");

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

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(clone));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.FindAsync([id], ct);
        if (invoice is null)
            return ServiceResult.NotFound("Invoice not found.");

        if (invoice.Status is not InvoiceStatus.Draft)
            return ServiceResult.Conflict(
                "Only draft invoices can be deleted. Cancel issued invoices instead.",
                "not_draft");

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string? NormalizeIban(string? iban) =>
        string.IsNullOrWhiteSpace(iban)
            ? null
            : iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}
