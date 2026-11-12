using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public sealed class PublicInvoiceService(
    AppDbContext db,
    IInvoicePdfService pdfs,
    IInvoiceIsdocService isdoc) : IPublicInvoiceService
{
    public async Task<ServiceResult<PublicInvoiceResponse>> GetByTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        var invoice = await FindPublicAsync(token, ct);
        if (invoice is null)
            return ServiceResult<PublicInvoiceResponse>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seller = string.IsNullOrWhiteSpace(invoice.SellerName)
            ? new InvoicePartyResponse("", null, null, null, null, null, null, null, null)
            : new InvoicePartyResponse(
                invoice.SellerName!,
                invoice.SellerIco,
                invoice.SellerDic,
                invoice.SellerIsVatPayer ? invoice.SellerIcDph : null,
                null,
                invoice.SellerStreet,
                invoice.SellerCity,
                invoice.SellerPostalCode,
                invoice.SellerCountry);

        var customer = new InvoicePartyResponse(
            invoice.CustomerName,
            invoice.CustomerIco,
            invoice.CustomerDic,
            invoice.CustomerIcDph,
            null,
            invoice.CustomerStreet,
            invoice.CustomerCity,
            invoice.CustomerPostalCode,
            invoice.CustomerCountry);

        var items = invoice.Items
            .OrderBy(i => i.SortOrder)
            .Select(item => new InvoiceItemResponse(
                item.Id,
                item.Description,
                item.Unit,
                item.Quantity,
                item.UnitPrice,
                item.VatRate,
                item.Subtotal,
                item.VatAmount,
                item.Total,
                item.SortOrder))
            .ToList();

        return ServiceResult<PublicInvoiceResponse>.Ok(new PublicInvoiceResponse(
            invoice.Number,
            invoice.DocumentType,
            invoice.Status,
            seller,
            customer,
            invoice.SellerIsVatPayer,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.DeliveryDate,
            invoice.PaidAt,
            invoice.Currency,
            invoice.VariableSymbol,
            invoice.ConstantSymbol,
            invoice.SpecificSymbol,
            invoice.PaymentMethod,
            invoice.BankIban,
            invoice.BankBic,
            invoice.Notes,
            invoice.Subtotal,
            invoice.VatTotal,
            invoice.Total,
            InvoiceMapper.IsOverdue(invoice, today),
            items));
    }

    public async Task<ServiceResult<InvoicePdfFile>> GetPdfByTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        var invoice = await FindPublicAsync(token, ct);
        if (invoice is null)
            return ServiceResult<InvoicePdfFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        return await pdfs.GenerateForCompanyAsync(invoice.Id, invoice.CompanyId, ct);
    }

    public async Task<ServiceResult<InvoiceIsdocFile>> GetIsdocByTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        var invoice = await FindPublicAsync(token, ct);
        if (invoice is null)
            return ServiceResult<InvoiceIsdocFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        return await isdoc.GenerateForCompanyAsync(invoice.Id, invoice.CompanyId, ct);
    }

    private async Task<Invoice?> FindPublicAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 16 or > 64)
            return null;

        return await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(
                i => i.PublicLinkEnabled
                     && i.PublicAccessToken == token
                     && i.Status != InvoiceStatus.Draft
                     && i.Status != InvoiceStatus.Cancelled,
                ct);
    }
}
