using Fakturio.Api.Caching;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Recurring;

public sealed class RecurringInvoiceService(
    AppDbContext db,
    ITenantContext tenant,
    IInvoiceNumberService numbers,
    ICacheInvalidator cache,
    IAuditLogService audit) : IRecurringInvoiceService
{
    public async Task<PagedResponse<RecurringInvoiceListItemResponse>> ListAsync(
        int page,
        int pageSize,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var companyId = await tenant.GetCompanyIdAsync(ct);

        var query = db.RecurringInvoices.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Items)
            .Where(r => r.CompanyId == companyId);

        if (activeOnly == true)
            query = query.Where(r => r.IsActive);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.NextRunOn)
            .ThenBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<RecurringInvoiceListItemResponse>(
            items.Select(ToListItem).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<RecurringInvoiceResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var entity = await db.RecurringInvoices.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);

        return entity is null ? null : ToResponse(entity);
    }

    public async Task<ServiceResult<RecurringInvoiceResponse>> CreateAsync(
        CreateRecurringInvoiceRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == request.CustomerId && c.CompanyId == companyId, ct);
        if (customer is null)
            return ServiceResult<RecurringInvoiceResponse>.BadRequest(
                "Customer not found.",
                ErrorCodes.CustomerNotFound);

        if (ServiceGuards.EnsureHasItems(request.Items?.Count ?? 0) is { } itemsError)
            return Fail<RecurringInvoiceResponse>(itemsError);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var day = ClampDay(request.DayOfMonth ?? 1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var next = request.NextRunOn ?? NextOccurrence(today, day, request.Interval, advanceIfPast: true);

        var entity = new RecurringInvoice
        {
            CompanyId = companyId,
            CustomerId = customer.Id,
            Name = request.Name.Trim(),
            Interval = request.Interval,
            DayOfMonth = day,
            DueDays = request.DueDays ?? company?.DefaultDueDays ?? 14,
            IsActive = request.IsActive ?? true,
            NextRunOn = next,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        ApplyItems(entity, request.Items!, company);
        db.RecurringInvoices.Add(entity);
        await db.SaveChangesAsync(ct);

        await db.Entry(entity).Reference(r => r.Customer).LoadAsync(ct);
        return ServiceResult<RecurringInvoiceResponse>.Created(ToResponse(entity));
    }

    public async Task<ServiceResult<RecurringInvoiceResponse>> UpdateAsync(
        Guid id,
        UpdateRecurringInvoiceRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var entity = await db.RecurringInvoices
            .Include(r => r.Items)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);
        if (entity is null)
            return ServiceResult<RecurringInvoiceResponse>.NotFound(
                "Recurring invoice not found.",
                ErrorCodes.NotFound);

        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == request.CustomerId && c.CompanyId == companyId, ct);
        if (customer is null)
            return ServiceResult<RecurringInvoiceResponse>.BadRequest(
                "Customer not found.",
                ErrorCodes.CustomerNotFound);

        if (ServiceGuards.EnsureHasItems(request.Items?.Count ?? 0) is { } itemsError)
            return Fail<RecurringInvoiceResponse>(itemsError);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        entity.CustomerId = customer.Id;
        entity.Name = request.Name.Trim();
        entity.Interval = request.Interval;
        entity.DayOfMonth = ClampDay(request.DayOfMonth ?? entity.DayOfMonth);
        entity.DueDays = request.DueDays ?? entity.DueDays;
        entity.IsActive = request.IsActive ?? entity.IsActive;
        if (request.NextRunOn.HasValue)
            entity.NextRunOn = request.NextRunOn.Value;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        db.RecurringInvoiceItems.RemoveRange(entity.Items);
        entity.Items.Clear();
        ApplyItems(entity, request.Items!, company);

        await db.SaveChangesAsync(ct);
        entity.Customer = customer;
        return ServiceResult<RecurringInvoiceResponse>.Ok(ToResponse(entity));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var entity = await db.RecurringInvoices
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);
        if (entity is null)
            return ServiceResult.NotFound("Recurring invoice not found.", ErrorCodes.NotFound);

        db.RecurringInvoices.Remove(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<ServiceResult<InvoiceResponse>> GenerateNowAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var template = await db.RecurringInvoices
            .Include(r => r.Items)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);
        if (template is null)
            return ServiceResult<InvoiceResponse>.NotFound(
                "Recurring invoice not found.",
                ErrorCodes.NotFound);

        var invoice = await GenerateFromTemplateAsync(template, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        if (invoice is null)
            return ServiceResult<InvoiceResponse>.Conflict(
                "Šablóna nemá položky.",
                ErrorCodes.Conflict);

        template.LastGeneratedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        template.LastInvoiceId = invoice.Id;
        template.NextRunOn = Advance(template.NextRunOn, template.DayOfMonth, template.Interval);
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await cache.InvalidateReportsAsync(ct);

        return ServiceResult<InvoiceResponse>.Created(
            InvoiceMapper.ToResponse(invoice));
    }

    public async Task<RecurringRunResult> ProcessDueAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = await db.RecurringInvoices
            .Include(r => r.Items)
            .Include(r => r.Customer)
            .Where(r => r.IsActive && r.NextRunOn <= today)
            .OrderBy(r => r.NextRunOn)
            .Take(200)
            .ToListAsync(ct);

        var generated = new List<Guid>();
        var skipped = 0;

        foreach (var template in due)
        {
            if (template.Items.Count == 0)
            {
                skipped++;
                template.NextRunOn = Advance(template.NextRunOn, template.DayOfMonth, template.Interval);
                template.UpdatedAt = DateTime.UtcNow;
                continue;
            }

            var invoice = await GenerateFromTemplateAsync(template, today, ct);
            if (invoice is null)
            {
                skipped++;
                continue;
            }

            generated.Add(invoice.Id);
            template.LastGeneratedOn = today;
            template.LastInvoiceId = invoice.Id;
            template.NextRunOn = Advance(template.NextRunOn, template.DayOfMonth, template.Interval);
            template.UpdatedAt = DateTime.UtcNow;
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);

        return new RecurringRunResult(generated.Count, skipped, generated);
    }

    private async Task<Invoice?> GenerateFromTemplateAsync(
        RecurringInvoice template,
        DateOnly issueDate,
        CancellationToken ct)
    {
        if (template.Items.Count == 0)
            return null;

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == template.CompanyId, ct);
        var customer = template.Customer
            ?? await db.Customers.FirstOrDefaultAsync(
                c => c.Id == template.CustomerId && c.CompanyId == template.CompanyId, ct);
        if (customer is null)
            return null;

        var number = await numbers.NextAsync(
            template.CompanyId,
            InvoiceDocumentType.Invoice,
            company?.InvoicePrefix,
            company?.ProformaPrefix,
            company?.CreditNotePrefix,
            ct);

        var isVatPayer = company?.IsVatPayer ?? true;
        var invoice = new Invoice
        {
            CompanyId = template.CompanyId,
            Number = number,
            DocumentType = InvoiceDocumentType.Invoice,
            Status = InvoiceStatus.Draft,
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(template.DueDays),
            Currency = company?.DefaultCurrency ?? "EUR",
            VariableSymbol = DigitsOnly(number),
            ConstantSymbol = company?.DefaultConstantSymbol,
            PaymentMethod = company?.DefaultPaymentMethod ?? "bank_transfer",
            BankIban = company?.BankIban,
            BankBic = company?.BankBic,
            Notes = template.Notes ?? company?.DefaultInvoiceNotes ?? company?.Notes,
        };

        InvoiceMapper.ApplyCustomerSnapshot(invoice, customer);
        InvoiceMapper.ApplySellerSnapshot(invoice, company);

        var sort = 0;
        foreach (var item in template.Items.OrderBy(i => i.SortOrder))
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = isVatPayer ? item.VatRate : 0m,
                SortOrder = sort++
            });
        }

        InvoiceCalculator.RecalculateTotals(invoice);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(
            AuditAction.Created,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"Opakovaná faktúra {invoice.Number} vytvorená zo šablóny „{template.Name}“.",
            new { recurringId = template.Id, template = template.Name },
            ct);

        return invoice;
    }

    private static void ApplyItems(
        RecurringInvoice entity,
        IReadOnlyList<RecurringInvoiceItemRequest> items,
        CompanySettings? company)
    {
        var isVatPayer = company?.IsVatPayer ?? true;
        var defaultVat = isVatPayer ? company?.DefaultVatRate ?? 23m : 0m;
        var sort = 0;
        foreach (var item in items)
        {
            entity.Items.Add(new RecurringInvoiceItem
            {
                ServiceCatalogItemId = item.ServiceCatalogItemId,
                Description = item.Description.Trim(),
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "ks" : item.Unit.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = isVatPayer ? item.VatRate ?? defaultVat : 0m,
                SortOrder = sort++
            });
        }
    }

    private static int ClampDay(int day) => Math.Clamp(day, 1, 28);

    private static DateOnly NextOccurrence(
        DateOnly from,
        int dayOfMonth,
        RecurrenceInterval interval,
        bool advanceIfPast)
    {
        var day = ClampDay(dayOfMonth);
        var candidate = new DateOnly(from.Year, from.Month, Math.Min(day, DateTime.DaysInMonth(from.Year, from.Month)));
        if (advanceIfPast && candidate < from)
            return Advance(candidate, day, interval);
        return candidate;
    }

    private static DateOnly Advance(DateOnly current, int dayOfMonth, RecurrenceInterval interval)
    {
        var day = ClampDay(dayOfMonth);
        var next = interval switch
        {
            RecurrenceInterval.Quarterly => current.AddMonths(3),
            RecurrenceInterval.Yearly => current.AddYears(1),
            _ => current.AddMonths(1),
        };
        var dim = DateTime.DaysInMonth(next.Year, next.Month);
        return new DateOnly(next.Year, next.Month, Math.Min(day, dim));
    }

    private static RecurringInvoiceListItemResponse ToListItem(RecurringInvoice r) => new(
        r.Id,
        r.Name,
        r.CustomerId,
        r.Customer?.Name ?? "",
        r.Interval,
        r.DayOfMonth,
        r.DueDays,
        r.IsActive,
        r.NextRunOn,
        r.LastGeneratedOn,
        r.LastInvoiceId,
        r.Items.Count,
        r.UpdatedAt);

    private static RecurringInvoiceResponse ToResponse(RecurringInvoice r) => new(
        r.Id,
        r.Name,
        r.CustomerId,
        r.Customer?.Name ?? "",
        r.Interval,
        r.DayOfMonth,
        r.DueDays,
        r.IsActive,
        r.NextRunOn,
        r.LastGeneratedOn,
        r.LastInvoiceId,
        r.Notes,
        r.Items.OrderBy(i => i.SortOrder).Select(i => new RecurringInvoiceItemResponse(
            i.Id,
            i.ServiceCatalogItemId,
            i.Description,
            i.Unit,
            i.Quantity,
            i.UnitPrice,
            i.VatRate,
            i.SortOrder)).ToList(),
        r.CreatedAt,
        r.UpdatedAt);

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());
}
