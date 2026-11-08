using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Admin;

public sealed class AdminService(
    AppDbContext db,
    IJobSettingsStore jobs,
    IAdminAccess adminAccess) : IAdminService
{
    public async Task<ServiceResult<AdminOverviewResponse>> GetOverviewAsync(
        CancellationToken ct = default)
    {
        bool databaseUp;
        try
        {
            databaseUp = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            databaseUp = false;
        }

        var users = await db.Users.AsNoTracking().CountAsync(ct);
        var companies = await db.CompanySettings.AsNoTracking().CountAsync(ct);
        var customers = await db.Customers.AsNoTracking().CountAsync(ct);
        var invoices = await db.Invoices.AsNoTracking().CountAsync(ct);
        var recurring = await db.RecurringInvoices.AsNoTracking().CountAsync(ct);
        var notes = await db.Notes.AsNoTracking().CountAsync(ct);
        var catalog = await db.ServiceCatalogItems.AsNoTracking().CountAsync(ct);
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

        var recentUsers = await BuildUserSummariesAsync(
            db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).Take(8),
            ct);

        return ServiceResult<AdminOverviewResponse>.Ok(new AdminOverviewResponse(
            Health: new AdminHealthDto(
                Status: databaseUp ? "ok" : "degraded",
                Database: databaseUp ? "up" : "down",
                Utc: DateTime.UtcNow),
            Counts: new AdminCountsDto(
                Users: users,
                Companies: companies,
                Customers: customers,
                Invoices: invoices,
                RecurringTemplates: recurring,
                Notes: notes,
                CatalogItems: catalog,
                AuditLogs: audits),
            Invoices: new AdminInvoicesDto(
                Draft: CountOf(InvoiceStatus.Draft),
                Issued: CountOf(InvoiceStatus.Issued),
                Paid: CountOf(InvoiceStatus.Paid),
                Overdue: CountOf(InvoiceStatus.Overdue),
                Cancelled: CountOf(InvoiceStatus.Cancelled),
                OpenAmount: SumOf(InvoiceStatus.Issued, InvoiceStatus.Overdue),
                PaidAmount: SumOf(InvoiceStatus.Paid),
                Currency: currency),
            RecentUsers: recentUsers,
            Jobs: jobs.GetSnapshot(),
            GeneratedAtUtc: DateTime.UtcNow));
    }

    public async Task<ServiceResult<PagedResponse<AdminUserSummaryDto>>> ListUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = ServiceGuards.NormalizeOptionalSearch(search);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (term is not null)
        {
            query = query.Where(u =>
                u.Email.Contains(term)
                || (u.FirstName != null && u.FirstName.Contains(term))
                || (u.LastName != null && u.LastName.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var pageQuery = query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var items = await BuildUserSummariesAsync(pageQuery, ct);

        return ServiceResult<PagedResponse<AdminUserSummaryDto>>.Ok(
            new PagedResponse<AdminUserSummaryDto>(
                items,
                page,
                pageSize,
                totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    private async Task<List<AdminUserSummaryDto>> BuildUserSummariesAsync(
        IQueryable<AppUser> usersQuery,
        CancellationToken ct)
    {
        var users = await usersQuery.ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();
        var companies = await db.CompanySettings.AsNoTracking()
            .Where(c => userIds.Contains(c.UserId))
            .Select(c => new { c.UserId, c.Name })
            .ToListAsync(ct);
        var companyByUser = companies
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return users.Select(u => new AdminUserSummaryDto(
            Id: u.Id,
            Email: u.Email,
            FirstName: u.FirstName,
            LastName: u.LastName,
            CreatedAt: u.CreatedAt,
            LastSeenAt: u.LastSeenAt,
            IsAdmin: adminAccess.IsAdminEmail(u.Email),
            CompanyName: companyByUser.TryGetValue(u.Id, out var name)
                ? (string.IsNullOrWhiteSpace(name) ? null : name)
                : null)).ToList();
    }
}
