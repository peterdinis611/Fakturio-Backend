using Fakturio.Api.Caching;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public class InvoiceService(
    AppDbContext db,
    IInvoiceNumberService numbers,
    ICacheInvalidator cache,
    IAuditLogService audit,
    ITenantContext tenant) : IInvoiceService
{
    public async Task<PagedResponse<InvoiceListItemResponse>> ListAsync(
        InvoiceListQuery query,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var (page, pageSize) = ServiceGuards.NormalizePaging(query.Page, query.PageSize);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var search = ServiceGuards.NormalizeOptionalSearch(query.Search);

        var invoices = db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId);

        if (query.Status.HasValue)
            invoices = invoices.Where(i => i.Status == query.Status.Value);

        if (query.DocumentType.HasValue)
            invoices = invoices.Where(i => i.DocumentType == query.DocumentType.Value);

        if (query.CustomerId.HasValue && query.CustomerId.Value != Guid.Empty)
            invoices = invoices.Where(i => i.CustomerId == query.CustomerId.Value);

        if (query.IssueFrom.HasValue && query.IssueTo.HasValue && query.IssueFrom > query.IssueTo)
        {
            return new PagedResponse<InvoiceListItemResponse>([], page, pageSize, 0, 0);
        }

        if (query.IssueFrom.HasValue)
            invoices = invoices.Where(i => i.IssueDate >= query.IssueFrom.Value);

        if (query.IssueTo.HasValue)
            invoices = invoices.Where(i => i.IssueDate <= query.IssueTo.Value);

        if (query.OverdueOnly == true)
        {
            invoices = invoices.Where(i =>
                i.DocumentType == InvoiceDocumentType.Invoice
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
                && i.DueDate < today
                && i.PaidAt == null);
        }

        if (search is not null)
        {
            invoices = invoices.Where(i =>
                i.Number.Contains(search) ||
                i.CustomerName.Contains(search) ||
                (i.VariableSymbol != null && i.VariableSymbol.Contains(search)) ||
                (i.CustomerIco != null && i.CustomerIco.Contains(search)) ||
                (i.RelatedInvoiceNumber != null && i.RelatedInvoiceNumber.Contains(search)));
        }

        var totalCount = await invoices.CountAsync(ct);
        var items = await invoices
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<InvoiceListItemResponse>(
            items.Select(i => InvoiceMapper.ToListItem(i, today)).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);

        return invoice is null ? null : InvoiceMapper.ToResponse(invoice);
    }

    public async Task<InvoiceResponse?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.CompanyId == companyId && i.Number == number.Trim(), ct);

        return invoice is null ? null : InvoiceMapper.ToResponse(invoice);
    }

    public async Task<ServiceResult<InvoiceResponse>> CreateAsync(
        CreateInvoiceRequest request,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateGuid(request.CustomerId, "Customer", ErrorCodes.EmptyGuid) is { } guidError)
            return Fail<InvoiceResponse>(guidError);

        if (ServiceGuards.EnsureHasItems(request.Items?.Count ?? 0) is { } itemsError)
            return Fail<InvoiceResponse>(itemsError);

        var documentType = request.DocumentType ?? InvoiceDocumentType.Invoice;
        if (documentType == InvoiceDocumentType.CreditNote)
            return ServiceResult<InvoiceResponse>.BadRequest(
                "Credit notes must be created from an existing invoice via POST /api/invoices/{id}/credit-note.",
                ErrorCodes.InvalidDocumentType);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var userId = await tenant.GetUserIdAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == request.CustomerId && c.CompanyId == companyId, ct);
        if (customer is null)
            return ServiceResult<InvoiceResponse>.BadRequest("Customer not found.", ErrorCodes.CustomerNotFound);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = company?.DefaultDueDays ?? 14;
        var dueDate = request.DueDate ?? issueDate.AddDays(dueDays);

        if (ServiceGuards.ValidateInvoiceDates(issueDate, dueDate, request.DeliveryDate) is { } dateError)
            return Fail<InvoiceResponse>(dateError);

        var isVatPayer = company?.IsVatPayer ?? true;
        var defaultVat = isVatPayer ? company?.DefaultVatRate ?? 23m : 0m;
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? company?.DefaultCurrency ?? "EUR"
            : request.Currency.Trim().ToUpperInvariant();

        var status = request.Status ?? InvoiceStatus.Issued;
        if (status is not (InvoiceStatus.Draft or InvoiceStatus.Issued))
            return ServiceResult<InvoiceResponse>.BadRequest(
                "New invoices can only be Draft or Issued.",
                ErrorCodes.InvalidStatus);

        var number = await numbers.NextAsync(
            companyId,
            documentType,
            company?.InvoicePrefix,
            company?.ProformaPrefix,
            company?.CreditNotePrefix,
            ct);

        var invoice = new Invoice
        {
            CompanyId = companyId,
            Number = number,
            DocumentType = documentType,
            Status = status,
            IssueDate = issueDate,
            DueDate = dueDate,
            DeliveryDate = request.DeliveryDate,
            Currency = currency,
            VariableSymbol = request.VariableSymbol ?? DigitsOnly(number),
            ConstantSymbol = request.ConstantSymbol ?? company?.DefaultConstantSymbol,
            SpecificSymbol = request.SpecificSymbol,
            PaymentMethod = request.PaymentMethod
                ?? company?.DefaultPaymentMethod
                ?? "bank_transfer",
            BankIban = NormalizeIban(request.BankIban) ?? company?.BankIban,
            BankBic = request.BankBic?.Trim().ToUpperInvariant() ?? company?.BankBic,
            Notes = request.Notes?.Trim()
                ?? company?.DefaultInvoiceNotes
                ?? company?.Notes,
            PaidAt = status == InvoiceStatus.Paid ? issueDate : null
        };

        InvoiceMapper.ApplyCustomerSnapshot(invoice, customer);
        InvoiceMapper.ApplySellerSnapshot(invoice, company);
        InvoiceMapper.ReplaceItems(invoice, request.Items!, defaultVat, forceZeroVat: !isVatPayer);
        InvoiceCalculator.RecalculateTotals(invoice);

        if (ServiceGuards.EnsurePositiveTotal(invoice.Total) is { } totalError)
            return Fail<InvoiceResponse>(totalError);

        db.Invoices.Add(invoice);
        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Created,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"{InvoiceMapper.DocumentTypeLabel(documentType)} {invoice.Number} vytvorená ({StatusSk(status)}).",
            new { documentType, status = status.ToString(), total = invoice.Total },
            ct);

        if (status == InvoiceStatus.Issued)
        {
            await audit.WriteAsync(
                AuditAction.Issued,
                "Invoice",
                invoice.Id,
                invoice.Number,
                $"{InvoiceMapper.DocumentTypeLabel(documentType)} {invoice.Number} vystavená.",
                null,
                ct);
        }

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceResponse>> UpdateAsync(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status is not InvoiceStatus.Draft)
            return ServiceResult<InvoiceResponse>.Conflict(
                "Only draft invoices can be fully updated.",
                ErrorCodes.NotDraft);

        if (ServiceGuards.ValidateGuid(request.CustomerId, "Customer", ErrorCodes.EmptyGuid) is { } guidError)
            return Fail<InvoiceResponse>(guidError);

        if (ServiceGuards.EnsureHasItems(request.Items?.Count ?? 0) is { } itemsError)
            return Fail<InvoiceResponse>(itemsError);

        if (ServiceGuards.ValidateInvoiceDates(request.IssueDate, request.DueDate, request.DeliveryDate) is { } dateError)
            return Fail<InvoiceResponse>(dateError);
        var customer = await db.Customers.FirstOrDefaultAsync(
            c => c.Id == request.CustomerId && c.CompanyId == companyId, ct);
        if (customer is null)
            return ServiceResult<InvoiceResponse>.BadRequest("Customer not found.", ErrorCodes.CustomerNotFound);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);

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
        InvoiceMapper.ApplySellerSnapshot(invoice, company);
        var isVatPayer = company?.IsVatPayer ?? invoice.SellerIsVatPayer;
        var defaultVat = isVatPayer ? company?.DefaultVatRate ?? 23m : 0m;
        InvoiceMapper.ReplaceItems(invoice, request.Items!, defaultVat, forceZeroVat: !isVatPayer);
        InvoiceCalculator.RecalculateTotals(invoice);

        if (ServiceGuards.EnsurePositiveTotal(invoice.Total) is { } totalError)
            return Fail<InvoiceResponse>(totalError);

        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Updated,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"{InvoiceMapper.DocumentTypeLabel(invoice.DocumentType)} {invoice.Number} upravená.",
            null,
            ct);

        return ServiceResult<InvoiceResponse>.Ok(InvoiceMapper.ToResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceStatusResponse>> UpdateStatusAsync(
        Guid id,
        UpdateInvoiceStatusRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        if (invoice is null)
            return ServiceResult<InvoiceStatusResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status == InvoiceStatus.Cancelled && request.Status != InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Cancelled invoices cannot change status. Use dedicated cancel/pay endpoints for lifecycle changes.",
                ErrorCodes.Cancelled);

        if (request.Status == InvoiceStatus.Paid)
            return await MarkPaidAsync(id, new MarkInvoicePaidRequest(null), ct);

        if (request.Status == InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceStatusResponse>.BadRequest(
                "Use POST /api/invoices/{id}/cancel with a reason.",
                ErrorCodes.UseCancelEndpoint);

        if (invoice.Status == InvoiceStatus.Paid && request.Status is InvoiceStatus.Draft)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Paid invoices cannot return to draft.",
                ErrorCodes.PaidToDraft);

        var previous = invoice.Status;
        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (request.Status != InvoiceStatus.Paid)
            invoice.PaidAt = null;

        await SaveAndInvalidateAsync(ct);

        if (request.Status == InvoiceStatus.Issued && previous == InvoiceStatus.Draft)
        {
            await audit.WriteAsync(
                AuditAction.Issued,
                "Invoice",
                invoice.Id,
                invoice.Number,
                $"{InvoiceMapper.DocumentTypeLabel(invoice.DocumentType)} {invoice.Number} vystavená.",
                new { from = previous.ToString(), to = request.Status.ToString() },
                ct);
        }

        return ServiceResult<InvoiceStatusResponse>.Ok(InvoiceMapper.ToStatusResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceStatusResponse>> MarkPaidAsync(
        Guid id,
        MarkInvoicePaidRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        if (invoice is null)
            return ServiceResult<InvoiceStatusResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status is InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Cancelled invoices cannot be marked as paid.",
                ErrorCodes.Cancelled);

        if (invoice.Status is InvoiceStatus.Draft)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Draft invoices must be issued before payment.",
                ErrorCodes.Draft);

        var paidAt = request.PaidAt ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (paidAt < invoice.IssueDate)
            return ServiceResult<InvoiceStatusResponse>.BadRequest(
                "Paid date cannot be before issue date.",
                ErrorCodes.InvalidPaidAt);

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = paidAt;
        invoice.CancelReason = null;
        invoice.CancelledAt = null;
        invoice.UpdatedAt = DateTime.UtcNow;

        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Paid,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"{InvoiceMapper.DocumentTypeLabel(invoice.DocumentType)} {invoice.Number} označená ako zaplatená ({paidAt:yyyy-MM-dd}).",
            new { paidAt = paidAt.ToString("yyyy-MM-dd") },
            ct);

        return ServiceResult<InvoiceStatusResponse>.Ok(InvoiceMapper.ToStatusResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceStatusResponse>> CancelAsync(
        Guid id,
        CancelInvoiceRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        if (invoice is null)
            return ServiceResult<InvoiceStatusResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status is InvoiceStatus.Paid)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Paid invoices cannot be cancelled.",
                ErrorCodes.Paid);

        if (invoice.Status is InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceStatusResponse>.Conflict(
                "Invoice is already cancelled.",
                ErrorCodes.AlreadyCancelled);

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelReason = request.Reason.Trim();
        invoice.CancelledAt = DateOnly.FromDateTime(DateTime.UtcNow);
        invoice.PaidAt = null;
        invoice.UpdatedAt = DateTime.UtcNow;

        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Cancelled,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"{InvoiceMapper.DocumentTypeLabel(invoice.DocumentType)} {invoice.Number} zrušená.",
            new { reason = invoice.CancelReason },
            ct);

        return ServiceResult<InvoiceStatusResponse>.Ok(InvoiceMapper.ToStatusResponse(invoice));
    }

    public async Task<ServiceResult<InvoiceResponse>> DuplicateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var source = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);

        if (source is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (source.DocumentType == InvoiceDocumentType.CreditNote)
            return ServiceResult<InvoiceResponse>.BadRequest(
                "Credit notes cannot be duplicated. Create a new credit note from the original invoice.",
                ErrorCodes.InvalidDocumentType);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = company?.DefaultDueDays ?? 14;
        var number = await numbers.NextAsync(
            companyId,
            source.DocumentType,
            company?.InvoicePrefix,
            company?.ProformaPrefix,
            company?.CreditNotePrefix,
            ct);

        var clone = CloneHeader(source, company, number, issueDate, dueDays);
        clone.DocumentType = source.DocumentType;
        clone.Status = InvoiceStatus.Draft;
        clone.RelatedInvoiceId = null;
        clone.RelatedInvoiceNumber = null;

        CopyItems(source, clone, company);
        InvoiceCalculator.RecalculateTotals(clone);
        db.Invoices.Add(clone);
        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Duplicated,
            "Invoice",
            clone.Id,
            clone.Number,
            $"{InvoiceMapper.DocumentTypeLabel(clone.DocumentType)} {clone.Number} duplikovaná z {source.Number}.",
            new { sourceId = source.Id, sourceNumber = source.Number },
            ct);

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(clone));
    }

    public async Task<ServiceResult<InvoiceResponse>> CreateCreditNoteAsync(
        Guid sourceId,
        CreateCreditNoteRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var source = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == sourceId && i.CompanyId == companyId, ct);

        if (source is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (source.DocumentType != InvoiceDocumentType.Invoice)
            return ServiceResult<InvoiceResponse>.BadRequest(
                "Credit notes can only be created from a tax invoice (not proforma or another credit note).",
                ErrorCodes.InvalidDocumentType);

        if (source.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            return ServiceResult<InvoiceResponse>.Conflict(
                "Credit notes require an issued, paid, or overdue invoice.",
                ErrorCodes.InvalidStatus);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = request.DueDate ?? issueDate;
        if (dueDate < issueDate)
            return ServiceResult<InvoiceResponse>.BadRequest(
                "Due date cannot be before issue date.",
                ErrorCodes.InvalidDateRange);

        var number = await numbers.NextAsync(
            companyId,
            InvoiceDocumentType.CreditNote,
            company?.InvoicePrefix,
            company?.ProformaPrefix,
            company?.CreditNotePrefix,
            ct);

        var credit = CloneHeader(source, company, number, issueDate, dueDays: 0);
        credit.DocumentType = InvoiceDocumentType.CreditNote;
        credit.Status = InvoiceStatus.Issued;
        credit.DueDate = dueDate;
        credit.RelatedInvoiceId = source.Id;
        credit.RelatedInvoiceNumber = source.Number;
        credit.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? $"Dobropis k faktúre {source.Number}"
            : request.Notes.Trim();
        credit.VariableSymbol = DigitsOnly(number);

        if (request.Items is { Count: > 0 })
        {
            var isVatPayer = company?.IsVatPayer ?? source.SellerIsVatPayer;
            var defaultVat = isVatPayer ? company?.DefaultVatRate ?? 23m : 0m;
            InvoiceMapper.ReplaceItems(credit, request.Items, defaultVat, forceZeroVat: !isVatPayer);
        }
        else
        {
            CopyItems(source, credit, company);
        }

        InvoiceCalculator.RecalculateTotals(credit);
        if (ServiceGuards.EnsurePositiveTotal(credit.Total) is { } totalError)
            return Fail<InvoiceResponse>(totalError);

        db.Invoices.Add(credit);
        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.CreditNoteCreated,
            "Invoice",
            credit.Id,
            credit.Number,
            $"Dobropis {credit.Number} k faktúre {source.Number}.",
            new { relatedInvoiceId = source.Id, relatedNumber = source.Number, total = credit.Total },
            ct);

        await audit.WriteAsync(
            AuditAction.Issued,
            "Invoice",
            credit.Id,
            credit.Number,
            $"Dobropis {credit.Number} vystavený.",
            null,
            ct);

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(credit));
    }

    public async Task<ServiceResult<InvoiceResponse>> FinalizeProformaAsync(
        Guid proformaId,
        FinalizeProformaRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var proforma = await db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == proformaId && i.CompanyId == companyId, ct);

        if (proforma is null)
            return ServiceResult<InvoiceResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (proforma.DocumentType != InvoiceDocumentType.Proforma)
            return ServiceResult<InvoiceResponse>.BadRequest(
                "Only a proforma (zálohová) can be finalized into a tax invoice.",
                ErrorCodes.InvalidDocumentType);

        if (proforma.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft)
            return ServiceResult<InvoiceResponse>.Conflict(
                "Proforma must be issued (or paid) before creating a tax invoice.",
                ErrorCodes.InvalidStatus);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = company?.DefaultDueDays ?? 14;
        var dueDate = request.DueDate ?? issueDate.AddDays(dueDays);

        var number = await numbers.NextAsync(
            companyId,
            InvoiceDocumentType.Invoice,
            company?.InvoicePrefix,
            company?.ProformaPrefix,
            company?.CreditNotePrefix,
            ct);

        var invoice = CloneHeader(proforma, company, number, issueDate, dueDays);
        invoice.DocumentType = InvoiceDocumentType.Invoice;
        invoice.Status = InvoiceStatus.Issued;
        invoice.DueDate = dueDate;
        invoice.RelatedInvoiceId = proforma.Id;
        invoice.RelatedInvoiceNumber = proforma.Number;
        invoice.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? $"Daňový doklad k zálohovej faktúre {proforma.Number}"
            : request.Notes.Trim();
        invoice.VariableSymbol = DigitsOnly(number);
        invoice.PaidAt = null;

        CopyItems(proforma, invoice, company);
        InvoiceCalculator.RecalculateTotals(invoice);

        db.Invoices.Add(invoice);

        if (proforma.Status != InvoiceStatus.Paid)
        {
            proforma.Status = InvoiceStatus.Paid;
            proforma.PaidAt = issueDate;
            proforma.UpdatedAt = DateTime.UtcNow;
        }

        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.ProformaFinalized,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"Faktúra {invoice.Number} vystavená zo zálohovej {proforma.Number}.",
            new { proformaId = proforma.Id, proformaNumber = proforma.Number },
            ct);

        await audit.WriteAsync(
            AuditAction.Issued,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"Faktúra {invoice.Number} vystavená.",
            null,
            ct);

        return ServiceResult<InvoiceResponse>.Created(InvoiceMapper.ToResponse(invoice));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        if (invoice is null)
            return ServiceResult.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status is not InvoiceStatus.Draft)
            return ServiceResult.Conflict(
                "Only draft invoices can be deleted. Cancel issued invoices instead.",
                ErrorCodes.NotDraft);

        var number = invoice.Number;
        var type = invoice.DocumentType;
        db.Invoices.Remove(invoice);
        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.Deleted,
            "Invoice",
            id,
            number,
            $"{InvoiceMapper.DocumentTypeLabel(type)} {number} zmazaná.",
            null,
            ct);

        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<InvoiceStatsResponse> GetStatsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var query = db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId)
            .Where(i => i.DocumentType != InvoiceDocumentType.Proforma)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);

        var invoices = await query.ToListAsync(ct);

        var draft = invoices.Count(i => i.Status == InvoiceStatus.Draft);
        var issued = invoices.Count(i => i.Status == InvoiceStatus.Issued);
        var paid = invoices.Count(i => i.Status == InvoiceStatus.Paid);
        var cancelled = invoices.Count(i => i.Status == InvoiceStatus.Cancelled);
        var overdue = invoices.Count(i => InvoiceMapper.IsOverdue(i, today) || i.Status == InvoiceStatus.Overdue);

        decimal SumSigned(IEnumerable<Invoice> list) =>
            list.Sum(InvoiceMapper.SignedTotal);

        var paidAmount = SumSigned(invoices.Where(i => i.Status == InvoiceStatus.Paid));
        var outstanding = SumSigned(invoices
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue));
        var overdueAmount = SumSigned(invoices
            .Where(i => InvoiceMapper.IsOverdue(i, today) || i.Status == InvoiceStatus.Overdue));

        return new InvoiceStatsResponse(
            from,
            to,
            invoices.Count,
            draft,
            issued,
            paid,
            overdue,
            cancelled,
            SumSigned(invoices.Where(i => i.Status != InvoiceStatus.Cancelled)),
            paidAmount,
            outstanding,
            overdueAmount,
            "EUR");
    }

    public async Task<IReadOnlyList<InvoiceReminderItemResponse>> GetRemindersAsync(
        int withinDays,
        bool onlyOverdue,
        CancellationToken ct = default)
    {
        withinDays = Math.Clamp(withinDays, 0, 90);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(withinDays);

        var query = db.Invoices
            .AsNoTracking()
            .Where(i =>
                i.DocumentType == InvoiceDocumentType.Invoice
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
                && i.PaidAt == null);

        // API calls are tenant-scoped; background jobs see all companies.
        try
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            query = query.Where(i => i.CompanyId == companyId);
        }
        catch (UnauthorizedAccessException)
        {
            // system job — no filter
        }

        if (onlyOverdue)
            query = query.Where(i => i.DueDate < today);
        else
            query = query.Where(i => i.DueDate <= until);

        var items = await query
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.Number)
            .ToListAsync(ct);

        return items.Select(i => InvoiceMapper.ToReminderItem(i, today)).ToList();
    }

    public async Task<ServiceResult<InvoiceReminderItemResponse>> MarkReminderSentAsync(
        Guid id,
        CancellationToken ct = default)
    {
        Invoice? invoice;
        try
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        }
        catch (UnauthorizedAccessException)
        {
            invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        }
        if (invoice is null)
            return ServiceResult<InvoiceReminderItemResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.DocumentType != InvoiceDocumentType.Invoice)
            return ServiceResult<InvoiceReminderItemResponse>.Conflict(
                "Reminders apply only to tax invoices.",
                ErrorCodes.InvalidDocumentType);

        if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.Overdue))
            return ServiceResult<InvoiceReminderItemResponse>.Conflict(
                "Reminders can only be sent for issued or overdue invoices.",
                ErrorCodes.InvalidStatus);

        invoice.LastReminderSentAt = DateTime.UtcNow;
        invoice.ReminderCount += 1;
        invoice.UpdatedAt = DateTime.UtcNow;
        await SaveAndInvalidateAsync(ct);

        await audit.WriteAsync(
            AuditAction.ReminderSent,
            "Invoice",
            invoice.Id,
            invoice.Number,
            $"Upomienka k faktúre {invoice.Number} označená ako odoslaná (#{invoice.ReminderCount}).",
            new { reminderCount = invoice.ReminderCount },
            ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return ServiceResult<InvoiceReminderItemResponse>.Ok(
            InvoiceMapper.ToReminderItem(invoice, today));
    }

    public async Task<SyncOverdueResponse> SyncOverdueAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdue = await db.Invoices
            .Where(i =>
                i.DocumentType == InvoiceDocumentType.Invoice
                && i.Status == InvoiceStatus.Issued
                && i.DueDate < today
                && i.PaidAt == null)
            .ToListAsync(ct);

        foreach (var invoice in overdue)
        {
            invoice.Status = InvoiceStatus.Overdue;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        if (overdue.Count > 0)
        {
            await SaveAndInvalidateAsync(ct);
            foreach (var invoice in overdue)
            {
                await audit.WriteAsync(
                    AuditAction.OverdueMarked,
                    "Invoice",
                    invoice.Id,
                    invoice.Number,
                    $"Faktúra {invoice.Number} označená ako po splatnosti.",
                    null,
                    ct);
            }
        }

        return new SyncOverdueResponse(overdue.Count);
    }

    private static Invoice CloneHeader(
        Invoice source,
        CompanySettings? company,
        string number,
        DateOnly issueDate,
        int dueDays) =>
        new()
        {
            CompanyId = source.CompanyId,
            Number = number,
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
            SellerIsVatPayer = company?.IsVatPayer ?? source.SellerIsVatPayer,
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

    private static void CopyItems(Invoice source, Invoice target, CompanySettings? company)
    {
        var forceZeroVat = !(company?.IsVatPayer ?? source.SellerIsVatPayer);
        if (forceZeroVat)
        {
            target.SellerIcDph = null;
            target.SellerIsVatPayer = false;
        }

        foreach (var item in source.Items.OrderBy(i => i.SortOrder))
        {
            target.Items.Add(new InvoiceItem
            {
                Description = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                VatRate = forceZeroVat ? 0m : item.VatRate,
                SortOrder = item.SortOrder
            });
        }
    }

    private async Task SaveAndInvalidateAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await cache.InvalidateReportsAsync(ct);
    }

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string? NormalizeIban(string? iban) =>
        string.IsNullOrWhiteSpace(iban)
            ? null
            : iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static string StatusSk(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "koncept",
        InvoiceStatus.Issued => "vystavená",
        InvoiceStatus.Paid => "zaplatená",
        InvoiceStatus.Cancelled => "zrušená",
        InvoiceStatus.Overdue => "po splatnosti",
        _ => status.ToString()
    };
}
