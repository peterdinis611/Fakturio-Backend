using System.Globalization;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Search;

public class SearchService(AppDbContext db, ITenantContext tenant) : ISearchService
{
    public async Task<ServiceResult<GlobalSearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken ct = default)
    {
        var termError = ServiceGuards.ValidateSearchTerm(request.Query);
        if (termError is not null)
            return Fail<GlobalSearchResponse>(termError);

        var types = request.Types is null or SearchEntityType.None
            ? SearchEntityType.All
            : request.Types.Value;

        if (types == SearchEntityType.None)
            return ServiceResult<GlobalSearchResponse>.BadRequest(
                "At least one search type is required.",
                ErrorCodes.InvalidSearchQuery);

        var (page, limit) = ServiceGuards.NormalizePaging(1, request.Limit, maxPageSize: 50);
        var term = request.Query.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hits = new List<SearchHitResponse>();

        if (types.HasFlag(SearchEntityType.Customers))
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            var customers = await db.Customers.Where(c => c.CompanyId == companyId)
                .AsNoTracking()
                .Where(c =>
                    c.Name.Contains(term) ||
                    (c.Ico != null && c.Ico.Contains(term)) ||
                    (c.Email != null && c.Email.Contains(term)) ||
                    (c.City != null && c.City.Contains(term)) ||
                    (c.Dic != null && c.Dic.Contains(term)))
                .OrderBy(c => c.Name)
                .Take(limit)
                .ToListAsync(ct);

            hits.AddRange(customers.Select(c => new SearchHitResponse(
                "customer",
                c.Id,
                c.Name,
                FormatCustomerSubtitle(c),
                null,
                c.Email,
                c.Ico,
                null,
                null,
                null,
                null,
                ScoreCustomer(c, term))));
        }

        if (types.HasFlag(SearchEntityType.Invoices))
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            var invoices = db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId);

            if (request.InvoiceStatus.HasValue)
                invoices = invoices.Where(i => i.Status == request.InvoiceStatus.Value);

            var matched = await invoices
                .Where(i =>
                    i.Number.Contains(term) ||
                    i.CustomerName.Contains(term) ||
                    (i.VariableSymbol != null && i.VariableSymbol.Contains(term)) ||
                    (i.CustomerIco != null && i.CustomerIco.Contains(term)) ||
                    (i.Notes != null && i.Notes.Contains(term)))
                .OrderByDescending(i => i.IssueDate)
                .ThenByDescending(i => i.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);

            hits.AddRange(matched.Select(i => new SearchHitResponse(
                "invoice",
                i.Id,
                i.Number,
                $"{i.CustomerName} · {i.Status}",
                i.Number,
                i.CustomerEmail,
                i.CustomerIco,
                i.Status,
                i.Total,
                i.Currency,
                i.IssueDate,
                ScoreInvoice(i, term))));
        }

        if (types.HasFlag(SearchEntityType.Notes))
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            var notes = await db.Notes.AsNoTracking().Where(n => n.CompanyId == companyId)
                .Where(n =>
                    n.Title.Contains(term) ||
                    n.Body.Contains(term))
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.UpdatedAt)
                .Take(limit)
                .ToListAsync(ct);

            hits.AddRange(notes.Select(n => new SearchHitResponse(
                "note",
                n.Id,
                n.Title,
                $"/notes/{n.Id:N}",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ScoreNote(n, term))));
        }

        if (types.HasFlag(SearchEntityType.Services))
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            var services = await db.ServiceCatalogItems.AsNoTracking().Where(s => s.CompanyId == companyId)
                .Where(s => s.Description.Contains(term))
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Description)
                .Take(limit)
                .ToListAsync(ct);

            hits.AddRange(services.Select(s => new SearchHitResponse(
                "service",
                s.Id,
                s.Description,
                $"{s.UnitPrice:0.00} {s.Unit} · DPH {s.VatRate:0.#}%",
                null,
                null,
                null,
                null,
                s.UnitPrice,
                null,
                null,
                ScoreService(s, term))));
        }

        if (types.HasFlag(SearchEntityType.Payments))
        {
            var companyId = await tenant.GetCompanyIdAsync(ct);
            var totalMatch = TryParseTotalSearch(term, out var totalValue);

            var payments = await db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId)
                .Where(i => i.Status == InvoiceStatus.Paid)
                .Where(i =>
                    i.Number.Contains(term) ||
                    i.CustomerName.Contains(term) ||
                    (i.VariableSymbol != null && i.VariableSymbol.Contains(term)) ||
                    (i.BankIban != null && i.BankIban.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (totalMatch && i.Total == totalValue))
                .OrderByDescending(i => i.PaidAt ?? i.IssueDate)
                .ThenByDescending(i => i.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);

            hits.AddRange(payments.Select(i => new SearchHitResponse(
                "payment",
                i.Id,
                i.Number,
                $"{i.CustomerName} · {i.Total:0.00} {i.Currency}",
                i.Number,
                i.CustomerEmail,
                i.CustomerIco,
                i.Status,
                i.Total,
                i.Currency,
                i.PaidAt ?? i.IssueDate,
                ScorePayment(i, term, totalValue, totalMatch))));
        }

        var ordered = hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title)
            .Take(limit)
            .ToList();

        return ServiceResult<GlobalSearchResponse>.Ok(new GlobalSearchResponse(
            term,
            types,
            limit,
            ordered.Count,
            ordered));
    }

    public async Task<ServiceResult<EntitySearchResponse<CustomerResponse>>> SearchCustomersAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var termError = ServiceGuards.ValidateSearchTerm(query);
        if (termError is not null)
            return Fail<EntitySearchResponse<CustomerResponse>>(termError);

        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = query.Trim();

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var q = db.Customers.AsNoTracking().Where(c => c.CompanyId == companyId &&
            c.Name.Contains(term) ||
            (c.Ico != null && c.Ico.Contains(term)) ||
            (c.Email != null && c.Email.Contains(term)) ||
            (c.City != null && c.City.Contains(term)) ||
            (c.Phone != null && c.Phone.Contains(term)) ||
            (c.Dic != null && c.Dic.Contains(term)));

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new { Customer = c, InvoiceCount = c.Invoices.Count })
            .ToListAsync(ct);

        return ServiceResult<EntitySearchResponse<CustomerResponse>>.Ok(
            new EntitySearchResponse<CustomerResponse>(
                term,
                page,
                pageSize,
                totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize),
                items.Select(x => CustomerMapper.ToResponse(x.Customer, x.InvoiceCount)).ToList()));
    }

    public async Task<ServiceResult<EntitySearchResponse<InvoiceListItemResponse>>> SearchInvoicesAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        InvoiceStatus? status = null,
        CancellationToken ct = default)
    {
        var termError = ServiceGuards.ValidateSearchTerm(query);
        if (termError is not null)
            return Fail<EntitySearchResponse<InvoiceListItemResponse>>(termError);

        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = query.Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var q = db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId &&
            i.Number.Contains(term) ||
            i.CustomerName.Contains(term) ||
            (i.VariableSymbol != null && i.VariableSymbol.Contains(term)) ||
            (i.CustomerIco != null && i.CustomerIco.Contains(term)) ||
            (i.BankIban != null && i.BankIban.Contains(term)) ||
            (i.Notes != null && i.Notes.Contains(term)));

        if (status.HasValue)
            q = q.Where(i => i.Status == status.Value);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ServiceResult<EntitySearchResponse<InvoiceListItemResponse>>.Ok(
            new EntitySearchResponse<InvoiceListItemResponse>(
                term,
                page,
                pageSize,
                totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize),
                items.Select(i => InvoiceMapper.ToListItem(i, today)).ToList()));
    }

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static string FormatCustomerSubtitle(Customer c)
    {
        var parts = new[] { c.Ico is null ? null : $"IČO {c.Ico}", c.City, c.Email }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" · ", parts);
    }

    private static double ScoreCustomer(Customer c, string term)
    {
        var t = term.ToLowerInvariant();
        double score = 0;
        if (c.Name.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 100;
        else if (c.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase)) score += 70;
        else if (c.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 40;
        if (c.Ico?.Contains(t, StringComparison.OrdinalIgnoreCase) == true) score += 60;
        if (c.Email?.Contains(t, StringComparison.OrdinalIgnoreCase) == true) score += 30;
        return score;
    }

    private static double ScoreInvoice(Invoice i, string term)
    {
        var t = term.ToLowerInvariant();
        double score = 0;
        if (i.Number.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 100;
        else if (i.Number.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 80;
        if (i.VariableSymbol?.Equals(term, StringComparison.OrdinalIgnoreCase) == true) score += 70;
        if (i.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 40;
        if (i.CustomerIco?.Contains(t, StringComparison.OrdinalIgnoreCase) == true) score += 50;
        return score;
    }

    private static double ScoreNote(Note n, string term)
    {
        double score = 0;
        if (n.Title.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 100;
        else if (n.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase)) score += 70;
        else if (n.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 45;
        if (n.Body.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 25;
        if (n.IsPinned) score += 5;
        return score;
    }

    private static double ScoreService(ServiceCatalogItem s, string term)
    {
        double score = 0;
        if (s.Description.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 100;
        else if (s.Description.StartsWith(term, StringComparison.OrdinalIgnoreCase)) score += 70;
        else if (s.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 40;
        if (!s.IsActive) score -= 10;
        return score;
    }

    private static double ScorePayment(Invoice i, string term, decimal totalValue, bool totalMatch)
    {
        var score = ScoreInvoice(i, term);
        if (totalMatch && i.Total == totalValue) score += 90;
        if (i.BankIban?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) score += 35;
        return score;
    }

    private static bool TryParseTotalSearch(string term, out decimal value)
    {
        value = 0;
        var cleaned = term.Trim()
            .Replace("€", "", StringComparison.Ordinal)
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "")
            .Replace("\u00a0", "");

        if (cleaned.Contains(',') && cleaned.Contains('.'))
            cleaned = cleaned.Replace(".", "").Replace(',', '.');
        else if (cleaned.Contains(','))
            cleaned = cleaned.Replace(',', '.');

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }
}
