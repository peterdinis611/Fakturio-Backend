using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Admin;

public sealed class AdminRaService(
    AppDbContext db,
    IAdminAccess adminAccess,
    IJobSettingsStore jobSettings) : IAdminRaService
{
    public async Task<(IReadOnlyList<AdminRaUserDto> Items, int Total)> ListUsersAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (term is not null)
        {
            query = query.Where(u =>
                u.Email.Contains(term)
                || (u.FirstName != null && u.FirstName.Contains(term))
                || (u.LastName != null && u.LastName.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = ApplyUserSort(query, sort, order);
        var users = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (await MapUsersAsync(users, ct), total);
    }

    public async Task<ServiceResult<AdminRaUserDto>> GetUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult<AdminRaUserDto>.NotFound("User not found.", ErrorCodes.NotFound);

        var mapped = await MapUsersAsync([user], ct);
        return ServiceResult<AdminRaUserDto>.Ok(mapped[0]);
    }

    public async Task<ServiceResult<AdminRaUserDto>> UpdateUserAsync(
        Guid id, AdminRaUpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult<AdminRaUserDto>.NotFound("User not found.", ErrorCodes.NotFound);

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return ServiceResult<AdminRaUserDto>.BadRequest("E-mail je povinný.", ErrorCodes.BadRequest);

            if (adminAccess.IsAdminEmail(email)
                && !string.Equals(user.Email, email, StringComparison.Ordinal))
            {
                return ServiceResult<AdminRaUserDto>.BadRequest(
                    "Tento e-mail nie je možné použiť.",
                    ErrorCodes.BadRequest);
            }

            var taken = await db.Users.AnyAsync(u => u.Id != id && u.Email == email, ct);
            if (taken)
                return ServiceResult<AdminRaUserDto>.Conflict(
                    "Účet s týmto e-mailom už existuje.",
                    ErrorCodes.DuplicateEmail);

            user.Email = email;
            user.SecurityStamp = Guid.NewGuid();
        }

        if (request.FirstName is not null)
            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        if (request.LastName is not null)
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var mapped = await MapUsersAsync([user], ct);
        return ServiceResult<AdminRaUserDto>.Ok(mapped[0]);
    }

    public async Task<ServiceResult> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.", ErrorCodes.NotFound);

        if (adminAccess.IsAdminEmail(user.Email))
            return ServiceResult.Conflict("Admin účet nie je možné zmazať.", ErrorCodes.Conflict);

        var remaining = await db.Users.CountAsync(ct);
        if (remaining <= 1)
            return ServiceResult.Conflict("Posledného používateľa nie je možné zmazať.", ErrorCodes.Conflict);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<ServiceResult> RevokeUserSessionsAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.", ErrorCodes.NotFound);

        user.SecurityStamp = Guid.NewGuid();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaCompanyDto> Items, int Total)> ListCompaniesAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.CompanySettings.AsNoTracking().AsQueryable();
        if (term is not null)
        {
            query = query.Where(c =>
                c.Name.Contains(term)
                || (c.Ico != null && c.Ico.Contains(term))
                || (c.Email != null && c.Email.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "name" => Ord(query, order, x => x.Name),
            "ico" => Ord(query, order, x => x.Ico ?? ""),
            "updatedat" => Ord(query, order, x => x.UpdatedAt),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var emails = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);

        var items = rows.Select(c => new AdminRaCompanyDto(
            c.Id, c.UserId,
            emails.TryGetValue(c.UserId, out var e) ? e : null,
            c.Name, c.Ico, c.Dic, c.IcDph, c.IsVatPayer, c.Email, c.City, c.Country,
            c.BankIban, !string.IsNullOrWhiteSpace(c.FioBankToken),
            c.CreatedAt, c.UpdatedAt)).ToList();

        return (items, total);
    }

    public async Task<ServiceResult<AdminRaCompanyDto>> GetCompanyAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return ServiceResult<AdminRaCompanyDto>.NotFound("Company not found.", ErrorCodes.NotFound);

        var email = await db.Users.AsNoTracking()
            .Where(u => u.Id == c.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        return ServiceResult<AdminRaCompanyDto>.Ok(new AdminRaCompanyDto(
            c.Id, c.UserId, email, c.Name, c.Ico, c.Dic, c.IcDph, c.IsVatPayer, c.Email, c.City,
            c.Country, c.BankIban, !string.IsNullOrWhiteSpace(c.FioBankToken),
            c.CreatedAt, c.UpdatedAt));
    }

    public async Task<ServiceResult<AdminRaCompanyDto>> UpdateCompanyAsync(
        Guid id, AdminRaUpdateCompanyRequest request, CancellationToken ct = default)
    {
        var company = await db.CompanySettings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (company is null)
            return ServiceResult<AdminRaCompanyDto>.NotFound("Company not found.", ErrorCodes.NotFound);

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return ServiceResult<AdminRaCompanyDto>.BadRequest("Názov firmy je povinný.", ErrorCodes.BadRequest);
            company.Name = name;
        }

        if (request.Email is not null)
            company.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.City is not null)
            company.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        if (request.Country is not null)
        {
            var country = request.Country.Trim().ToUpperInvariant();
            if (country.Length != 2)
                return ServiceResult<AdminRaCompanyDto>.BadRequest(
                    "Krajina musí byť 2-písmenový kód.", ErrorCodes.BadRequest);
            company.Country = country;
        }

        if (request.IcDph is not null)
            company.IcDph = string.IsNullOrWhiteSpace(request.IcDph)
                ? null
                : request.IcDph.Trim().ToUpperInvariant();

        if (request.IsVatPayer is not null)
            company.IsVatPayer = request.IsVatPayer.Value;

        if (company.IsVatPayer && string.IsNullOrWhiteSpace(company.IcDph))
        {
            return ServiceResult<AdminRaCompanyDto>.BadRequest(
                "IČ DPH je povinné pri platcovi DPH. Doplň IČ DPH pred zapnutím režimu.",
                ErrorCodes.BadRequest);
        }

        if (!company.IsVatPayer)
            company.DefaultVatRate = 0m;

        company.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetCompanyAsync(id, ct);
    }

    public async Task<(IReadOnlyList<AdminRaInvoiceDto> Items, int Total)> ListInvoicesAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, string? status = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(i => i.CompanyId == companyId);
        var statusTerm = Normalize(status);
        if (statusTerm is not null)
        {
            if (Enum.TryParse<InvoiceStatus>(statusTerm, ignoreCase: true, out var parsed))
                query = query.Where(i => i.Status == parsed);
            else
                query = query.Where(_ => false);
        }

        if (term is not null)
        {
            query = query.Where(i =>
                i.Number.Contains(term)
                || i.CustomerName.Contains(term)
                || (i.CustomerIco != null && i.CustomerIco.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "number" => Ord(query, order, x => x.Number),
            "total" => Ord(query, order, x => x.Total),
            "duedate" => Ord(query, order, x => x.DueDate),
            "status" => Ord(query, order, x => x.Status),
            _ => Ord(query, order, x => x.IssueDate),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var companyIds = rows.Select(r => r.CompanyId).Distinct().ToList();
        var names = await db.CompanySettings.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rows.Select(i => new AdminRaInvoiceDto(
            i.Id, i.CompanyId,
            names.TryGetValue(i.CompanyId, out var n) ? n : null,
            i.Number, i.DocumentType.ToString(), i.Status.ToString(), i.Flow.ToString(),
            i.CustomerName, i.IssueDate, i.DueDate, i.Currency, i.Total, i.CreatedAt)).ToList();

        return (items, total);
    }

    public async Task<ServiceResult<AdminRaInvoiceDto>> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var i = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (i is null)
            return ServiceResult<AdminRaInvoiceDto>.NotFound("Invoice not found.", ErrorCodes.NotFound);

        var name = await db.CompanySettings.AsNoTracking()
            .Where(c => c.Id == i.CompanyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);

        return ServiceResult<AdminRaInvoiceDto>.Ok(new AdminRaInvoiceDto(
            i.Id, i.CompanyId, name, i.Number, i.DocumentType.ToString(), i.Status.ToString(),
            i.Flow.ToString(), i.CustomerName, i.IssueDate, i.DueDate, i.Currency, i.Total, i.CreatedAt));
    }

    public async Task<ServiceResult> DeleteInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .Include(i => i.Items)
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is null)
            return ServiceResult.NotFound("Invoice not found.", ErrorCodes.NotFound);

        // RelatedInvoice is Restrict — detach dependants (credit notes / finalized proformas).
        var dependants = await db.Invoices
            .Where(i => i.RelatedInvoiceId == id)
            .ToListAsync(ct);
        foreach (var dep in dependants)
            dep.RelatedInvoiceId = null;

        var templates = await db.RecurringInvoices
            .Where(r => r.LastInvoiceId == id)
            .ToListAsync(ct);
        foreach (var template in templates)
            template.LastInvoiceId = null;

        db.InvoiceItems.RemoveRange(invoice.Items);
        db.InvoiceAttachments.RemoveRange(invoice.Attachments);
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaCustomerDto> Items, int Total)> ListCustomersAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(c => c.CompanyId == companyId);
        if (term is not null)
        {
            query = query.Where(c =>
                c.Name.Contains(term)
                || (c.Ico != null && c.Ico.Contains(term))
                || (c.Email != null && c.Email.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "name" => Ord(query, order, x => x.Name),
            "ico" => Ord(query, order, x => x.Ico ?? ""),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var companyIds = rows.Select(r => r.CompanyId).Distinct().ToList();
        var names = await db.CompanySettings.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rows.Select(c => new AdminRaCustomerDto(
            c.Id, c.CompanyId,
            names.TryGetValue(c.CompanyId, out var n) ? n : null,
            c.Name, c.Ico, c.Email, c.City, c.Country, c.CreatedAt)).ToList();

        return (items, total);
    }

    public async Task<ServiceResult<AdminRaCustomerDto>> GetCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return ServiceResult<AdminRaCustomerDto>.NotFound("Customer not found.", ErrorCodes.NotFound);

        var name = await db.CompanySettings.AsNoTracking()
            .Where(x => x.Id == c.CompanyId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        return ServiceResult<AdminRaCustomerDto>.Ok(new AdminRaCustomerDto(
            c.Id, c.CompanyId, name, c.Name, c.Ico, c.Email, c.City, c.Country, c.CreatedAt));
    }

    public async Task<(IReadOnlyList<AdminRaAuditLogDto> Items, int Total)> ListAuditLogsAsync(
        int skip, int take, string? sort, string? order, string? q, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (term is not null)
        {
            query = query.Where(a =>
                a.Summary.Contains(term)
                || a.EntityType.Contains(term)
                || (a.ActorEmail != null && a.ActorEmail.Contains(term))
                || (a.EntityNumber != null && a.EntityNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "action" => Ord(query, order, x => x.Action),
            "entitytype" => Ord(query, order, x => x.EntityType),
            _ => Ord(query, order, x => x.OccurredAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var items = rows.Select(MapAudit).ToList();
        return (items, total);
    }

    public async Task<ServiceResult<AdminRaAuditLogDto>> GetAuditLogAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null)
            return ServiceResult<AdminRaAuditLogDto>.NotFound("Audit log not found.", ErrorCodes.NotFound);

        return ServiceResult<AdminRaAuditLogDto>.Ok(MapAudit(a));
    }

    public async Task<(IReadOnlyList<AdminRaNoteDto> Items, int Total)> ListNotesAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.Notes.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(n => n.CompanyId == companyId);
        if (term is not null)
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "title" => Ord(query, order, x => x.Title),
            "ispinned" => Ord(query, order, x => x.IsPinned),
            "updatedat" => Ord(query, order, x => x.UpdatedAt),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var names = await CompanyNamesAsync(rows.Select(r => r.CompanyId), ct);
        return (rows.Select(n => MapNote(n, names)).ToList(), total);
    }

    public async Task<ServiceResult<AdminRaNoteDto>> GetNoteAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null)
            return ServiceResult<AdminRaNoteDto>.NotFound("Note not found.", ErrorCodes.NotFound);

        var names = await CompanyNamesAsync([n.CompanyId], ct);
        return ServiceResult<AdminRaNoteDto>.Ok(MapNote(n, names));
    }

    public async Task<ServiceResult> DeleteNoteAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null)
            return ServiceResult.NotFound("Note not found.", ErrorCodes.NotFound);

        db.Notes.Remove(n);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaCatalogItemDto> Items, int Total)> ListCatalogItemsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.ServiceCatalogItems.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(c => c.CompanyId == companyId);
        if (term is not null)
            query = query.Where(c => c.Description.Contains(term) || c.Unit.Contains(term));

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "description" => Ord(query, order, x => x.Description),
            "unitprice" => Ord(query, order, x => x.UnitPrice),
            "sortorder" => Ord(query, order, x => x.SortOrder),
            "isactive" => Ord(query, order, x => x.IsActive),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        var names = await CompanyNamesAsync(rows.Select(r => r.CompanyId), ct);
        return (rows.Select(c => MapCatalogItem(c, names)).ToList(), total);
    }

    public async Task<ServiceResult<AdminRaCatalogItemDto>> GetCatalogItemAsync(
        Guid id, CancellationToken ct = default)
    {
        var c = await db.ServiceCatalogItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return ServiceResult<AdminRaCatalogItemDto>.NotFound("Catalog item not found.", ErrorCodes.NotFound);

        var names = await CompanyNamesAsync([c.CompanyId], ct);
        return ServiceResult<AdminRaCatalogItemDto>.Ok(MapCatalogItem(c, names));
    }

    public async Task<ServiceResult> DeleteCatalogItemAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return ServiceResult.NotFound("Catalog item not found.", ErrorCodes.NotFound);

        db.ServiceCatalogItems.Remove(c);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaRecurringDto> Items, int Total)> ListRecurringAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.RecurringInvoices.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(r => r.CompanyId == companyId);
        if (term is not null)
            query = query.Where(r => r.Name.Contains(term) || r.Customer.Name.Contains(term));

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "name" => Ord(query, order, x => x.Name),
            "nextrunon" => Ord(query, order, x => x.NextRunOn),
            "isactive" => Ord(query, order, x => x.IsActive),
            "interval" => Ord(query, order, x => x.Interval),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (await MapRecurringAsync(rows, ct), total);
    }

    public async Task<ServiceResult<AdminRaRecurringDto>> GetRecurringAsync(
        Guid id, CancellationToken ct = default)
    {
        var r = await db.RecurringInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return ServiceResult<AdminRaRecurringDto>.NotFound("Recurring invoice not found.", ErrorCodes.NotFound);

        var mapped = await MapRecurringAsync([r], ct);
        return ServiceResult<AdminRaRecurringDto>.Ok(mapped[0]);
    }

    public async Task<ServiceResult> DeleteRecurringAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.RecurringInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
            return ServiceResult.NotFound("Recurring invoice not found.", ErrorCodes.NotFound);

        db.RecurringInvoiceItems.RemoveRange(r.Items);
        db.RecurringInvoices.Remove(r);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaNotificationDto> Items, int Total)> ListNotificationsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.Notifications.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(n => n.CompanyId == companyId);
        if (term is not null)
        {
            query = query.Where(n =>
                n.Title.Contains(term)
                || n.Body.Contains(term)
                || (n.InvoiceNumber != null && n.InvoiceNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "title" => Ord(query, order, x => x.Title),
            "kind" => Ord(query, order, x => x.Kind),
            "isread" => Ord(query, order, x => x.IsRead),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (await MapNotificationsAsync(rows, ct), total);
    }

    public async Task<ServiceResult<AdminRaNotificationDto>> GetNotificationAsync(
        Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null)
            return ServiceResult<AdminRaNotificationDto>.NotFound("Notification not found.", ErrorCodes.NotFound);

        var mapped = await MapNotificationsAsync([n], ct);
        return ServiceResult<AdminRaNotificationDto>.Ok(mapped[0]);
    }

    public async Task<ServiceResult> DeleteNotificationAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null)
            return ServiceResult.NotFound("Notification not found.", ErrorCodes.NotFound);

        db.Notifications.Remove(n);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<(IReadOnlyList<AdminRaAttachmentDto> Items, int Total)> ListAttachmentsAsync(
        int skip, int take, string? sort, string? order, string? q,
        Guid? companyId = null, CancellationToken ct = default)
    {
        (skip, take) = Clamp(skip, take);
        var term = Normalize(q);
        var query = db.InvoiceAttachments.AsNoTracking().AsQueryable();
        if (companyId is not null)
            query = query.Where(a => a.CompanyId == companyId);
        if (term is not null)
            query = query.Where(a => a.FileName.Contains(term) || a.Invoice.Number.Contains(term));

        var total = await query.CountAsync(ct);
        query = (sort?.ToLowerInvariant()) switch
        {
            "filename" => Ord(query, order, x => x.FileName),
            "sizebytes" => Ord(query, order, x => x.SizeBytes),
            "contenttype" => Ord(query, order, x => x.ContentType),
            _ => Ord(query, order, x => x.CreatedAt),
        };

        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (await MapAttachmentsAsync(rows, ct), total);
    }

    public async Task<ServiceResult<AdminRaAttachmentDto>> GetAttachmentAsync(
        Guid id, CancellationToken ct = default)
    {
        var a = await db.InvoiceAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null)
            return ServiceResult<AdminRaAttachmentDto>.NotFound("Attachment not found.", ErrorCodes.NotFound);

        var mapped = await MapAttachmentsAsync([a], ct);
        return ServiceResult<AdminRaAttachmentDto>.Ok(mapped[0]);
    }

    public async Task<ServiceResult<AdminRaDashboardDto>> GetDashboardAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().CountAsync(ct);
        var companies = await db.CompanySettings.AsNoTracking().CountAsync(ct);
        var customers = await db.Customers.AsNoTracking().CountAsync(ct);
        var invoices = await db.Invoices.AsNoTracking().CountAsync(ct);
        var recurring = await db.RecurringInvoices.AsNoTracking().CountAsync(ct);
        var notes = await db.Notes.AsNoTracking().CountAsync(ct);
        var catalog = await db.ServiceCatalogItems.AsNoTracking().CountAsync(ct);
        var notifications = await db.Notifications.AsNoTracking().CountAsync(ct);
        var attachments = await db.InvoiceAttachments.AsNoTracking().CountAsync(ct);
        var audits = await db.AuditLogs.AsNoTracking().CountAsync(ct);

        var statusGroups = await db.Invoices.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        int CountOf(InvoiceStatus status) =>
            statusGroups.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        decimal SumOf(params InvoiceStatus[] statuses) =>
            statusGroups.Where(x => statuses.Contains(x.Status)).Sum(x => x.Total);

        var currency = await db.Invoices.AsNoTracking()
            .OrderByDescending(i => i.IssueDate)
            .Select(i => i.Currency)
            .FirstOrDefaultAsync(ct) ?? "EUR";

        return ServiceResult<AdminRaDashboardDto>.Ok(new AdminRaDashboardDto(
            Id: "current",
            Users: users,
            Companies: companies,
            Customers: customers,
            Invoices: invoices,
            RecurringTemplates: recurring,
            Notes: notes,
            CatalogItems: catalog,
            Notifications: notifications,
            Attachments: attachments,
            AuditLogs: audits,
            DraftInvoices: CountOf(InvoiceStatus.Draft),
            IssuedInvoices: CountOf(InvoiceStatus.Issued),
            PaidInvoices: CountOf(InvoiceStatus.Paid),
            OverdueInvoices: CountOf(InvoiceStatus.Overdue),
            CancelledInvoices: CountOf(InvoiceStatus.Cancelled),
            OpenAmount: SumOf(InvoiceStatus.Issued, InvoiceStatus.Overdue),
            PaidAmount: SumOf(InvoiceStatus.Paid),
            Currency: currency,
            GeneratedAtUtc: DateTime.UtcNow));
    }

    public Task<ServiceResult<AdminRaJobSettingsDto>> GetJobSettingsAsync(CancellationToken ct = default)
        => Task.FromResult(ServiceResult<AdminRaJobSettingsDto>.Ok(
            new AdminRaJobSettingsDto("current", jobSettings.GetSnapshot())));

    public async Task<ServiceResult<AdminRaJobSettingsDto>> UpdateJobSettingsAsync(
        AdminRaJobSettingsDto request, CancellationToken ct = default)
    {
        var s = request.Settings;
        if (s?.OverdueSync is null || s.ReminderJob is null || s.RecurringInvoices is null)
            return ServiceResult<AdminRaJobSettingsDto>.BadRequest(
                "Job settings sú povinné.", ErrorCodes.BadRequest);

        var snapshot = await jobSettings.UpdateAsync(
            new UpdateJobSettingsRequest(s.OverdueSync, s.ReminderJob, s.RecurringInvoices), ct);
        return ServiceResult<AdminRaJobSettingsDto>.Ok(new AdminRaJobSettingsDto("current", snapshot));
    }

    private async Task<Dictionary<Guid, string>> CompanyNamesAsync(
        IEnumerable<Guid> companyIds, CancellationToken ct)
    {
        var ids = companyIds.Distinct().ToList();
        return await db.CompanySettings.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }

    private static string? NameOf(Dictionary<Guid, string> names, Guid id) =>
        names.TryGetValue(id, out var n) ? n : null;

    private static AdminRaNoteDto MapNote(Note n, Dictionary<Guid, string> names) => new(
        n.Id, n.CompanyId, NameOf(names, n.CompanyId), n.Title, n.Body, n.IsPinned,
        n.CreatedAt, n.UpdatedAt);

    private static AdminRaCatalogItemDto MapCatalogItem(ServiceCatalogItem c, Dictionary<Guid, string> names) => new(
        c.Id, c.CompanyId, NameOf(names, c.CompanyId), c.Description, c.Unit, c.UnitPrice,
        c.VatRate, c.IsActive, c.SortOrder, c.CreatedAt, c.UpdatedAt);

    private async Task<List<AdminRaRecurringDto>> MapRecurringAsync(
        List<RecurringInvoice> rows, CancellationToken ct)
    {
        var names = await CompanyNamesAsync(rows.Select(r => r.CompanyId), ct);
        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var recurringIds = rows.Select(r => r.Id).ToList();
        var itemCounts = await db.RecurringInvoiceItems.AsNoTracking()
            .Where(i => recurringIds.Contains(i.RecurringInvoiceId))
            .GroupBy(i => i.RecurringInvoiceId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return rows.Select(r => new AdminRaRecurringDto(
            r.Id, r.CompanyId, NameOf(names, r.CompanyId), r.CustomerId,
            customers.TryGetValue(r.CustomerId, out var cn) ? cn : null,
            r.Name, r.Interval.ToString(), r.DayOfMonth, r.DueDays, r.IsActive,
            r.NextRunOn, r.LastGeneratedOn, r.LastInvoiceId,
            itemCounts.TryGetValue(r.Id, out var count) ? count : 0,
            r.Notes, r.CreatedAt, r.UpdatedAt)).ToList();
    }

    private async Task<List<AdminRaNotificationDto>> MapNotificationsAsync(
        List<AppNotification> rows, CancellationToken ct)
    {
        var names = await CompanyNamesAsync(rows.Select(r => r.CompanyId), ct);
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var emails = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);

        return rows.Select(n => new AdminRaNotificationDto(
            n.Id, n.UserId, emails.TryGetValue(n.UserId, out var e) ? e : null,
            n.CompanyId, NameOf(names, n.CompanyId), n.Kind.ToString(), n.Title, n.Body,
            n.Href, n.InvoiceId, n.InvoiceNumber, n.IsRead, n.CreatedAt, n.ReadAt)).ToList();
    }

    private async Task<List<AdminRaAttachmentDto>> MapAttachmentsAsync(
        List<InvoiceAttachment> rows, CancellationToken ct)
    {
        var names = await CompanyNamesAsync(rows.Select(r => r.CompanyId), ct);
        var invoiceIds = rows.Select(r => r.InvoiceId).Distinct().ToList();
        var numbers = await db.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Number })
            .ToDictionaryAsync(x => x.Id, x => x.Number, ct);

        return rows.Select(a => new AdminRaAttachmentDto(
            a.Id, a.CompanyId, NameOf(names, a.CompanyId), a.InvoiceId,
            numbers.TryGetValue(a.InvoiceId, out var num) ? num : null,
            a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt)).ToList();
    }

    private async Task<List<AdminRaUserDto>> MapUsersAsync(List<AppUser> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var companies = await db.CompanySettings.AsNoTracking()
            .Where(c => ids.Contains(c.UserId))
            .Select(c => new { c.Id, c.UserId, c.Name })
            .ToListAsync(ct);
        var byUser = companies
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        return users.Select(u =>
        {
            byUser.TryGetValue(u.Id, out var company);
            return new AdminRaUserDto(
                u.Id, u.Email, u.FirstName, u.LastName, u.CreatedAt, u.UpdatedAt, u.LastSeenAt,
                adminAccess.IsAdminEmail(u.Email),
                string.IsNullOrWhiteSpace(company?.Name) ? null : company.Name,
                company?.Id);
        }).ToList();
    }

    private static AdminRaAuditLogDto MapAudit(AuditLogEntry a) => new(
        a.Id, a.CompanyId, a.Action.ToString(), a.EntityType, a.EntityId, a.EntityNumber,
        a.ActorEmail, a.ActorDisplayName, a.Summary, a.OccurredAt);

    private static IQueryable<AppUser> ApplyUserSort(IQueryable<AppUser> query, string? sort, string? order) =>
        (sort?.ToLowerInvariant()) switch
        {
            "email" => Ord(query, order, x => x.Email),
            "lastname" => Ord(query, order, x => x.LastName ?? ""),
            "lastseenat" => Ord(query, order, x => x.LastSeenAt ?? DateTime.MinValue),
            _ => Ord(query, order, x => x.CreatedAt),
        };

    private static IQueryable<T> Ord<T, TKey>(IQueryable<T> q, string? order, System.Linq.Expressions.Expression<Func<T, TKey>> key) =>
        string.Equals(order, "ASC", StringComparison.OrdinalIgnoreCase)
            ? q.OrderBy(key)
            : q.OrderByDescending(key);

    private static (int Skip, int Take) Clamp(int skip, int take)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take <= 0 ? 25 : take, 1, 200);
        return (skip, take);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
