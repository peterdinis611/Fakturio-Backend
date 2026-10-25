using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Reports;

public class ReportService(AppDbContext db) : IReportService
{
    private const string DefaultCurrency = "EUR";

    public async Task<ReportOverviewResponse> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var today = Today();
        var invoices = await LoadInvoicesAsync(from, to, includeItems: false, ct);

        var active = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
        var paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var outstanding = invoices
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
            .ToList();
        var overdue = invoices.Where(i => InvoiceMapper.IsOverdue(i, today) || i.Status == InvoiceStatus.Overdue)
            .ToList();

        var issuedAmount = active.Sum(i => i.Total);
        var paidAmount = paid.Sum(i => i.Total);
        var outstandingAmount = outstanding.Sum(i => i.Total);
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
                return new StatusBreakdownItem(status, group.Count, group.Sum(i => i.Total));
            })
            .Where(x => x.Count > 0)
            .ToList();

        return new ReportOverviewResponse(
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
            overdue.Sum(i => i.Total),
            invoices.Where(i => i.Status == InvoiceStatus.Cancelled).Sum(i => i.Total),
            active.Count == 0
                ? 0
                : Math.Round(active.Average(i => i.Total), 2, MidpointRounding.AwayFromZero),
            paymentDays.Count == 0 ? null : Math.Round(paymentDays.Average(), 1),
            collectionRate,
            DefaultCurrency,
            byStatus);
    }

    public async Task<MonthlyRevenueResponse> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default)
    {
        var selectedYear = year ?? DateTime.UtcNow.Year;
        var from = new DateOnly(selectedYear, 1, 1);
        var to = new DateOnly(selectedYear, 12, 31);

        var invoices = await db.Invoices
            .AsNoTracking()
            .Where(i =>
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
                issued.Sum(i => i.Total),
                paid.Count,
                paid.Sum(i => i.Total),
                outstanding.Sum(i => i.Total));
        }).ToList();

        return new MonthlyRevenueResponse(
            selectedYear,
            DefaultCurrency,
            months,
            months.Sum(m => m.IssuedAmount),
            months.Sum(m => m.PaidAmount));
    }

    public async Task<AgingReportResponse> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default)
    {
        largestLimit = Math.Clamp(largestLimit, 1, 50);
        var today = Today();

        var open = await db.Invoices
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

        return new AgingReportResponse(
            today,
            DefaultCurrency,
            open.Sum(i => i.Total),
            buckets,
            largest);
    }

    public async Task<TopCustomersReportResponse> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var invoices = (await LoadInvoicesAsync(from, to, includeItems: false, ct))
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
            .ToList();

        var totalRevenue = invoices.Sum(i => i.Total);
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
                var amount = list.Sum(i => i.Total);

                return new TopCustomerItem(
                    g.Key.CustomerId,
                    g.Key.CustomerName,
                    g.Key.CustomerIco,
                    list.Count,
                    paid.Count,
                    outstanding.Count,
                    amount,
                    paid.Sum(i => i.Total),
                    outstanding.Sum(i => i.Total),
                    paymentDays.Count == 0 ? null : Math.Round(paymentDays.Average(), 1),
                    totalRevenue == 0
                        ? 0
                        : Math.Round(amount / totalRevenue * 100m, 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(c => c.TotalAmount)
            .ThenBy(c => c.CustomerName)
            .Take(limit)
            .ToList();

        return new TopCustomersReportResponse(from, to, DefaultCurrency, customers);
    }

    public async Task<VatReportResponse> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var invoices = (await LoadInvoicesAsync(from, to, includeItems: true, ct))
            .Where(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.Paid or InvoiceStatus.Overdue)
            .ToList();

        var lines = invoices
            .SelectMany(i => i.Items.Select(item => new
            {
                InvoiceId = i.Id,
                item.VatRate,
                TaxBase = item.Subtotal,
                Vat = item.VatAmount,
                Gross = item.Total
            }))
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

        return new VatReportResponse(
            from,
            to,
            DefaultCurrency,
            byRate.Sum(x => x.TaxBase),
            byRate.Sum(x => x.VatAmount),
            byRate.Sum(x => x.GrossAmount),
            byRate);
    }

    private async Task<List<Invoice>> LoadInvoicesAsync(
        DateOnly? from,
        DateOnly? to,
        bool includeItems,
        CancellationToken ct)
    {
        IQueryable<Invoice> query = db.Invoices.AsNoTracking();
        if (includeItems)
            query = query.Include(i => i.Items);

        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);

        return await query.ToListAsync(ct);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
