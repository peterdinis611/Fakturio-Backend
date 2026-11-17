using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

/// <summary>
/// react-admin simple-rest compatible endpoints (Content-Range + JSON array).
/// Proxied from Next.js at /api/ra/* with the httpOnly JWT cookie.
/// </summary>
[Authorize(Policy = "Admin")]
[Route("api/admin/ra")]
public sealed class AdminRaController(IAdminRaService admin) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListUsersAsync(start, take, sort, order, q, ct);
        return Range("users", start, items, total);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminRaUserDto>> GetUser(Guid id, CancellationToken ct)
        => (await admin.GetUserAsync(id, ct)).ToActionResult(this);

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AdminRaUserDto>> UpdateUser(
        Guid id, [FromBody] AdminRaUpdateUserRequest request, CancellationToken ct)
        => (await admin.UpdateUserAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult> DeleteUser(Guid id, CancellationToken ct)
        => (await admin.DeleteUserAsync(id, ct)).ToActionResult(this);

    [HttpPost("users/{id:guid}/revoke-sessions")]
    public async Task<ActionResult> RevokeSessions(Guid id, CancellationToken ct)
        => (await admin.RevokeUserSessionsAsync(id, ct)).ToActionResult(this);

    [HttpGet("companies")]
    public async Task<IActionResult> ListCompanies(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListCompaniesAsync(start, take, sort, order, q, ct);
        return Range("companies", start, items, total);
    }

    [HttpGet("companies/{id:guid}")]
    public async Task<ActionResult<AdminRaCompanyDto>> GetCompany(Guid id, CancellationToken ct)
        => (await admin.GetCompanyAsync(id, ct)).ToActionResult(this);

    [HttpPut("companies/{id:guid}")]
    public async Task<ActionResult<AdminRaCompanyDto>> UpdateCompany(
        Guid id, [FromBody] AdminRaUpdateCompanyRequest request, CancellationToken ct)
        => (await admin.UpdateCompanyAsync(id, request, ct)).ToActionResult(this);

    [HttpGet("invoices")]
    public async Task<IActionResult> ListInvoices(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListInvoicesAsync(
            start, take, sort, order, q, companyId, status, ct);
        return Range("invoices", start, items, total);
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<AdminRaInvoiceDto>> GetInvoice(Guid id, CancellationToken ct)
        => (await admin.GetInvoiceAsync(id, ct)).ToActionResult(this);

    [HttpDelete("invoices/{id:guid}")]
    public async Task<ActionResult> DeleteInvoice(Guid id, CancellationToken ct)
        => (await admin.DeleteInvoiceAsync(id, ct)).ToActionResult(this);

    [HttpGet("customers")]
    public async Task<IActionResult> ListCustomers(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListCustomersAsync(start, take, sort, order, q, companyId, ct);
        return Range("customers", start, items, total);
    }

    [HttpGet("customers/{id:guid}")]
    public async Task<ActionResult<AdminRaCustomerDto>> GetCustomer(Guid id, CancellationToken ct)
        => (await admin.GetCustomerAsync(id, ct)).ToActionResult(this);

    [HttpGet("audit-logs")]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListAuditLogsAsync(start, take, sort, order, q, ct);
        return Range("audit-logs", start, items, total);
    }

    [HttpGet("audit-logs/{id:guid}")]
    public async Task<ActionResult<AdminRaAuditLogDto>> GetAuditLog(Guid id, CancellationToken ct)
        => (await admin.GetAuditLogAsync(id, ct)).ToActionResult(this);

    [HttpGet("notes")]
    public async Task<IActionResult> ListNotes(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListNotesAsync(start, take, sort, order, q, companyId, ct);
        return Range("notes", start, items, total);
    }

    [HttpGet("notes/{id:guid}")]
    public async Task<ActionResult<AdminRaNoteDto>> GetNote(Guid id, CancellationToken ct)
        => (await admin.GetNoteAsync(id, ct)).ToActionResult(this);

    [HttpDelete("notes/{id:guid}")]
    public async Task<ActionResult> DeleteNote(Guid id, CancellationToken ct)
        => (await admin.DeleteNoteAsync(id, ct)).ToActionResult(this);

    [HttpGet("catalog-items")]
    public async Task<IActionResult> ListCatalogItems(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListCatalogItemsAsync(start, take, sort, order, q, companyId, ct);
        return Range("catalog-items", start, items, total);
    }

    [HttpGet("catalog-items/{id:guid}")]
    public async Task<ActionResult<AdminRaCatalogItemDto>> GetCatalogItem(Guid id, CancellationToken ct)
        => (await admin.GetCatalogItemAsync(id, ct)).ToActionResult(this);

    [HttpDelete("catalog-items/{id:guid}")]
    public async Task<ActionResult> DeleteCatalogItem(Guid id, CancellationToken ct)
        => (await admin.DeleteCatalogItemAsync(id, ct)).ToActionResult(this);

    [HttpGet("recurring-invoices")]
    public async Task<IActionResult> ListRecurring(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListRecurringAsync(start, take, sort, order, q, companyId, ct);
        return Range("recurring-invoices", start, items, total);
    }

    [HttpGet("recurring-invoices/{id:guid}")]
    public async Task<ActionResult<AdminRaRecurringDto>> GetRecurring(Guid id, CancellationToken ct)
        => (await admin.GetRecurringAsync(id, ct)).ToActionResult(this);

    [HttpDelete("recurring-invoices/{id:guid}")]
    public async Task<ActionResult> DeleteRecurring(Guid id, CancellationToken ct)
        => (await admin.DeleteRecurringAsync(id, ct)).ToActionResult(this);

    [HttpGet("notifications")]
    public async Task<IActionResult> ListNotifications(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListNotificationsAsync(start, take, sort, order, q, companyId, ct);
        return Range("notifications", start, items, total);
    }

    [HttpGet("notifications/{id:guid}")]
    public async Task<ActionResult<AdminRaNotificationDto>> GetNotification(Guid id, CancellationToken ct)
        => (await admin.GetNotificationAsync(id, ct)).ToActionResult(this);

    [HttpDelete("notifications/{id:guid}")]
    public async Task<ActionResult> DeleteNotification(Guid id, CancellationToken ct)
        => (await admin.DeleteNotificationAsync(id, ct)).ToActionResult(this);

    [HttpGet("attachments")]
    public async Task<IActionResult> ListAttachments(
        [FromQuery(Name = "_start")] int start = 0,
        [FromQuery(Name = "_end")] int end = 25,
        [FromQuery(Name = "_sort")] string? sort = null,
        [FromQuery(Name = "_order")] string? order = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? companyId = null,
        CancellationToken ct = default)
    {
        var take = Math.Max(0, end - start);
        var (items, total) = await admin.ListAttachmentsAsync(start, take, sort, order, q, companyId, ct);
        return Range("attachments", start, items, total);
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult<AdminRaAttachmentDto>> GetAttachment(Guid id, CancellationToken ct)
        => (await admin.GetAttachmentAsync(id, ct)).ToActionResult(this);

    /// <summary>Single object (no Content-Range). <c>dashboard/current</c> serves react-admin getOne.</summary>
    [HttpGet("dashboard")]
    [HttpGet("dashboard/current")]
    public async Task<ActionResult<AdminRaDashboardDto>> GetDashboard(CancellationToken ct)
        => (await admin.GetDashboardAsync(ct)).ToActionResult(this);

    [HttpGet("job-settings/current")]
    public async Task<ActionResult<AdminRaJobSettingsDto>> GetJobSettings(CancellationToken ct)
        => (await admin.GetJobSettingsAsync(ct)).ToActionResult(this);

    [HttpPut("job-settings/current")]
    public async Task<ActionResult<AdminRaJobSettingsDto>> UpdateJobSettings(
        [FromBody] AdminRaJobSettingsDto request, CancellationToken ct)
        => (await admin.UpdateJobSettingsAsync(request, ct)).ToActionResult(this);

    private OkObjectResult Range<T>(string resource, int start, IReadOnlyList<T> items, int total)
    {
        var end = items.Count == 0 ? start : start + items.Count - 1;
        Response.Headers.Append("Access-Control-Expose-Headers", "Content-Range");
        Response.Headers["Content-Range"] = $"{resource} {start}-{end}/{total}";
        return Ok(items);
    }
}
