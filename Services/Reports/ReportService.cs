using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Reports;

public class ReportService(AppDbContext db, ITenantContext tenant) : IReportService
{
    private const string DefaultCurrency = "EUR";

    public async Task<ServiceResult<ReportOverviewResponse>> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<ReportOverviewResponse>(rangeError);

        var today = Today();
        var invoices = await LoadInvoicesAsync(from, to, includeItems: false, ct);

        var active = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
        var paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var outstanding = invoices
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
            .ToList();
        var overdue = invoices.Where(i => InvoiceMapper.IsOverdue(i, today) || i.Status == InvoiceStatus.Overdue)
            .ToList();

        var issuedAmount = active.Sum(InvoiceMapper.SignedTotal);
        var paidAmount = paid.Sum(InvoiceMapper.SignedTotal);
        var outstandingAmount = outstanding.Sum(InvoiceMapper.SignedTotal);
        var collectionRate = issuedAmount == 0
            ? 0m
            : Math.Round(paidAmount / issuedAmount * 100m, 2, MidpointRounding.AwayFromZero);

        var paymentDays = paid
            .Where(i => i.PaidAt.HasValue)
            .Select(i => (double)(i.PaidAt!.Value.DayNumber - i.IssueDate.DayNumber))
            .Where(d => d >= 0)
            .ToList();

        var byStatus = Enum.GetValues<InvoiceStatus>()
            .Select(status =>
            {
                var group = invoices.Where(i => i.Status == status).ToList();
                return new StatusBreakdownItem(status, group.Count, group.Sum(InvoiceMapper.SignedTotal));
            })
            .Where(x => x.Count > 0)
            .ToList();

        return Ok(new ReportOverviewResponse(
            from,
            to,
            today,
            invoices.Count,
            invoices.Count(i => i.Status == InvoiceStatus.Draft),
            invoices.Count(i => i.Status == InvoiceStatus.Issued),
            paid.Count,
            overdue.Count,
            invoices.Count(i => i.Status == InvoiceStatus.Cancelled),
            issuedAmount,
            paidAmount,
            outstandingAmount,
            overdue.Sum(InvoiceMapper.SignedTotal),
            invoices.Where(i => i.Status == InvoiceStatus.Cancelled).Sum(InvoiceMapper.SignedTotal),
            active.Count == 0
                ? 0
                : Math.Round(active.Average(i => InvoiceMapper.SignedTotal(i)), 2, MidpointRounding.AwayFromZero),
            paymentDays.Count == 0 ? null : Math.Round(paymentDays.Average(), 1),
            collectionRate,
            DefaultCurrency,
            byStatus));
    }

    public async Task<ServiceResult<MonthlyRevenueResponse>> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;
        if (ServiceGuards.ValidateYear(selectedYear) is { } yearError)
            return Fail<MonthlyRevenueResponse>(yearError);
        var from = new DateOnly(selectedYear, 1, 1);
        var to = new DateOnly(selectedYear, 12, 31);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoices = await db.Invoices.Where(i => i.CompanyId == companyId)
            .AsNoTracking()
            .Where(i =>
                i.DocumentType != InvoiceDocumentType.Proforma &&
                i.Status != InvoiceStatus.Cancelled &&
                ((i.IssueDate >= from && i.IssueDate <= to) ||
                 (i.PaidAt != null && i.PaidAt >= from && i.PaidAt <= to)))
            .ToListAsync(ct);

        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var issued = invoices
                .Where(i => i.IssueDate.Year == selectedYear && i.IssueDate.Month == month)
                .Where(i => i.Status != InvoiceStatus.Draft)
                .ToList();

            var paid = invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                            && i.PaidAt.HasValue
                            && i.PaidAt.Value.Year == selectedYear
                            && i.PaidAt.Value.Month == month)
                .ToList();

            var outstanding = invoices
                .Where(i => i.IssueDate.Year == selectedYear
                            && i.IssueDate.Month == month
                            && i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
                .ToList();

            return new MonthlyRevenueItem(
                selectedYear,
                month,
                $"{selectedYear}-{month:D2}",
                issued.Count,
                issued.Sum(InvoiceMapper.SignedTotal),
                paid.Count,
                paid.Sum(InvoiceMapper.SignedTotal),
                outstanding.Sum(InvoiceMapper.SignedTotal));
        }).ToList();

        return Ok(new MonthlyRevenueResponse(
            selectedYear,
            DefaultCurrency,
            months,
            months.Sum(m => m.IssuedAmount),
            months.Sum(m => m.PaidAmount)));
    }

    public async Task<ServiceResult<AgingReportResponse>> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default)
    {
        if (largestLimit < 1 || largestLimit > 50)
            return ServiceResult<AgingReportResponse>.BadRequest(
                "largestLimit must be between 1 and 50.",
                ErrorCodes.BadRequest);

        largestLimit = Math.Clamp(largestLimit, 1, 50);
        var today = Today();

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var open = await db.Invoices.Where(i => i.CompanyId == companyId)
            .AsNoTracking()
            .Where(i =>
                (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
                && i.PaidAt == null)
            .ToListAsync(ct);

        var definitions = new (string Key, string Label, int From, int? To)[]
        {
            ("current", "Current (not overdue)", int.MinValue, 0),
            ("1_30", "1–30 days", 1, 30),
            ("31_60", "31–60 days", 31, 60),
            ("61_90", "61–90 days", 61, 90),
            ("90_plus", "90+ days", 91, null)
        };

        var buckets = definitions.Select(def =>
        {
            var items = open.Where(i =>
            {
                var days = today.DayNumber - i.DueDate.DayNumber;
                if (def.To is null)
                    return days >= def.From;
                if (def.From == int.MinValue)
                    return days <= def.To!.Value;
                return days >= def.From && days <= def.To.Value;
            }).ToList();

            return new AgingBucketItem(
                def.Key,
                def.Label,
                def.From == int.MinValue ? 0 : def.From,
                def.To,
                items.Count,
                items.Sum(i => i.Total));
        }).ToList();

        var largest = open
            .OrderByDescending(i => i.Total)
            .ThenBy(i => i.DueDate)
            .Take(largestLimit)
            .Select(i => new AgingInvoiceItem(
                i.Id,
                i.Number,
                i.CustomerName,
                i.DueDate,
                Math.Max(0, today.DayNumber - i.DueDate.DayNumber),
                i.Total,
                i.Currency,
                i.Status))
            .ToList();

        return Ok(new AgingReportResponse(
            today,
            DefaultCurrency,
            open.Sum(i => i.Total),
            buckets,
            largest));
    }

    public async Task<ServiceResult<TopCustomersReportResponse>> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<TopCustomersReportResponse>(rangeError);

        if (limit < 1 || limit > 50)
            return ServiceResult<TopCustomersReportResponse>.BadRequest(
                "limit must be between 1 and 50.",
                ErrorCodes.BadRequest);

        limit = Math.Clamp(limit, 1, 50);
        var invoices = (await LoadInvoicesAsync(from, to, includeItems: false, ct))
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
            .ToList();

        var totalRevenue = invoices.Sum(InvoiceMapper.SignedTotal);
        var customers = invoices
            .GroupBy(i => new { i.CustomerId, i.CustomerName, i.CustomerIco })
            .Select(g =>
            {
                var list = g.ToList();
                var paid = list.Where(i => i.Status == InvoiceStatus.Paid).ToList();
                var outstanding = list
                    .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
                    .ToList();
                var paymentDays = paid
                    .Where(i => i.PaidAt.HasValue)
                    .Select(i => (double)(i.PaidAt!.Value.DayNumber - i.IssueDate.DayNumber))
                    .Where(d => d >= 0)
                    .ToList();
                var amount = list.Sum(InvoiceMapper.SignedTotal);

                return new TopCustomerItem(
                    g.Key.CustomerId,
                    g.Key.CustomerName,
                    g.Key.CustomerIco,
                    list.Count,
                    paid.Count,
                    outstanding.Count,
                    amount,
                    paid.Sum(InvoiceMapper.SignedTotal),
                    outstanding.Sum(InvoiceMapper.SignedTotal),
                    paymentDays.Count == 0 ? null : Math.Round(paymentDays.Average(), 1),
                    totalRevenue == 0
                        ? 0
                        : Math.Round(amount / totalRevenue * 100m, 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(c => c.TotalAmount)
            .ThenBy(c => c.CustomerName)
            .Take(limit)
            .ToList();

        return Ok(new TopCustomersReportResponse(from, to, DefaultCurrency, customers));
    }

    public async Task<ServiceResult<VatReportResponse>> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<VatReportResponse>(rangeError);

        var invoices = (await LoadInvoicesAsync(from, to, includeItems: true, ct))
            .Where(i =>
                i.DocumentType != InvoiceDocumentType.Proforma
                && i.Status is InvoiceStatus.Issued or InvoiceStatus.Paid or InvoiceStatus.Overdue)
            .ToList();

        var lines = invoices
            .SelectMany(i =>
            {
                var sign = i.DocumentType == InvoiceDocumentType.CreditNote ? -1m : 1m;
                return i.Items.Select(item => new
                {
                    InvoiceId = i.Id,
                    item.VatRate,
                    TaxBase = item.Subtotal * sign,
                    Vat = item.VatAmount * sign,
                    Gross = item.Total * sign
                });
            })
            .ToList();

        var byRate = lines
            .GroupBy(l => l.VatRate)
            .OrderBy(g => g.Key)
            .Select(g => new VatRateBreakdownItem(
                g.Key,
                g.Count(),
                g.Select(x => x.InvoiceId).Distinct().Count(),
                g.Sum(x => x.TaxBase),
                g.Sum(x => x.Vat),
                g.Sum(x => x.Gross)))
            .ToList();

        return Ok(new VatReportResponse(
            from,
            to,
            DefaultCurrency,
            byRate.Sum(x => x.TaxBase),
            byRate.Sum(x => x.VatAmount),
            byRate.Sum(x => x.GrossAmount),
            byRate));
    }

    private async Task<List<Invoice>> LoadInvoicesAsync(
        DateOnly? from,
        DateOnly? to,
        bool includeItems,
        CancellationToken ct)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        IQueryable<Invoice> query = db.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.DocumentType != InvoiceDocumentType.Proforma);
        if (includeItems)
            query = query.Include(i => i.Items);

        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);

        return await query.ToListAsync(ct);
    }

    private static ServiceResult<T> Ok<T>(T data) => ServiceResult<T>.Ok(data);

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
