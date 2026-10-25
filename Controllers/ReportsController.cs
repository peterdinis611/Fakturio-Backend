using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    /// <summary>Dashboard overview: counts, amounts, collection rate, avg days to pay.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ReportOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportOverviewResponse>> Overview(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => Ok(await reports.GetOverviewAsync(from, to, ct));

    /// <summary>Monthly issued vs paid revenue for a calendar year.</summary>
    [HttpGet("revenue/monthly")]
    [ProducesResponseType(typeof(MonthlyRevenueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyRevenueResponse>> MonthlyRevenue(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await reports.GetMonthlyRevenueAsync(year, ct));

    /// <summary>Outstanding invoices aging buckets + largest unpaid.</summary>
    [HttpGet("aging")]
    [ProducesResponseType(typeof(AgingReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgingReportResponse>> Aging(
        [FromQuery] int largestLimit = 10,
        CancellationToken ct = default)
        => Ok(await reports.GetAgingAsync(largestLimit, ct));

    /// <summary>Top customers by revenue share in period.</summary>
    [HttpGet("customers/top")]
    [ProducesResponseType(typeof(TopCustomersReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TopCustomersReportResponse>> TopCustomers(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
        => Ok(await reports.GetTopCustomersAsync(from, to, limit, ct));

    /// <summary>VAT breakdown by rate for issued/paid/overdue invoices.</summary>
    [HttpGet("vat")]
    [ProducesResponseType(typeof(VatReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VatReportResponse>> Vat(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => Ok(await reports.GetVatSummaryAsync(from, to, ct));
}
