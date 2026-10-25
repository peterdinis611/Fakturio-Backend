using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/search")]
public sealed class SearchController(ISearchService search) : ApiControllerBase
{
    /// <summary>Global search across customers and invoices.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GlobalSearchResponse>> Search(
        [FromQuery] string q,
        [FromQuery] SearchEntityType? types = null,
        [FromQuery] int limit = 20,
        [FromQuery] InvoiceStatus? invoiceStatus = null,
        CancellationToken ct = default)
        => (await search.SearchAsync(new SearchRequest(q, types, limit, invoiceStatus), ct))
            .ToActionResult(this);

    [HttpGet("customers")]
    [ProducesResponseType(typeof(EntitySearchResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntitySearchResponse<CustomerResponse>>> SearchCustomers(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => (await search.SearchCustomersAsync(q, page, pageSize, ct)).ToActionResult(this);

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(EntitySearchResponse<InvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntitySearchResponse<InvoiceListItemResponse>>> SearchInvoices(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InvoiceStatus? status = null,
        CancellationToken ct = default)
        => (await search.SearchInvoicesAsync(q, page, pageSize, status, ct)).ToActionResult(this);
}
