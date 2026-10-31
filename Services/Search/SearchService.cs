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
}
