using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ReportOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportOverviewResponse>> Overview(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => (await reports.GetOverviewAsync(from, to, ct)).ToActionResult(this);

    [HttpGet("revenue/monthly")]
    [ProducesResponseType(typeof(MonthlyRevenueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyRevenueResponse>> MonthlyRevenue(
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => (await reports.GetMonthlyRevenueAsync(year, ct)).ToActionResult(this);

    [HttpGet("aging")]
    [ProducesResponseType(typeof(AgingReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgingReportResponse>> Aging(
        [FromQuery] int largestLimit = 10,
        CancellationToken ct = default)
        => (await reports.GetAgingAsync(largestLimit, ct)).ToActionResult(this);

    [HttpGet("customers/top")]
    [ProducesResponseType(typeof(TopCustomersReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TopCustomersReportResponse>> TopCustomers(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
        => (await reports.GetTopCustomersAsync(from, to, limit, ct)).ToActionResult(this);

    [HttpGet("vat")]
    [ProducesResponseType(typeof(VatReportResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VatReportResponse>> Vat(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => (await reports.GetVatSummaryAsync(from, to, ct)).ToActionResult(this);
}
