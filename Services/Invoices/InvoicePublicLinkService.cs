using System.Security.Cryptography;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public sealed class InvoicePublicLinkService(
    AppDbContext db,
    ITenantContext tenant) : IInvoicePublicLinkService
{
    public async Task<ServiceResult<PublicLinkResponse>> GetAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices.AsNoTracking()
            .Where(i => i.Id == invoiceId && i.CompanyId == companyId)
            .Select(i => new { i.PublicLinkEnabled, i.PublicAccessToken, i.Status })
            .FirstOrDefaultAsync(ct);

        if (invoice is null)
            return ServiceResult<PublicLinkResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        return ServiceResult<PublicLinkResponse>.Ok(ToResponse(invoice.PublicLinkEnabled, invoice.PublicAccessToken));
    }

    public async Task<ServiceResult<PublicLinkResponse>> SetAsync(
        Guid invoiceId,
        SetPublicLinkRequest request,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<PublicLinkResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        if (invoice.Status == InvoiceStatus.Draft)
            return ServiceResult<PublicLinkResponse>.BadRequest(
                "Public link is only available for issued documents.",
                ErrorCodes.BadRequest);

        if (request.Enabled)
        {
            invoice.PublicLinkEnabled = true;
            invoice.PublicAccessToken ??= GenerateToken();
        }
        else
        {
            invoice.PublicLinkEnabled = false;
            // Keep token so re-enable restores the same URL; clear only if you prefer rotate-on-disable.
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<PublicLinkResponse>.Ok(
            ToResponse(invoice.PublicLinkEnabled, invoice.PublicAccessToken));
    }

    private static PublicLinkResponse ToResponse(bool enabled, string? token) =>
        new(
            enabled,
            enabled ? token : null,
            enabled && !string.IsNullOrWhiteSpace(token) ? $"/p/{token}" : null);

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
