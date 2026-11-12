using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Invoices.Pdf;
using Fakturio.Api.Services.Payments;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices;

public class InvoicePdfService(
    AppDbContext db,
    ITenantContext tenant,
    ILogoStorageService logos) : IInvoicePdfService
{
    public async Task<ServiceResult<InvoicePdfFile>> GenerateAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await GenerateForCompanyAsync(invoiceId, companyId, ct);
    }

    public async Task<ServiceResult<InvoicePdfFile>> GenerateForCompanyAsync(
        Guid invoiceId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<InvoicePdfFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var template = InvoicePdfTemplates.Normalize(company?.InvoicePdfTemplate);
        var legalClauses = InvoiceLegalTexts.BuildClauses(invoice, company?.CommercialRegister);
        var logoBytes = await logos.TryReadImageBytesAsync(company?.LogoUrl, ct);

        PaymentQrResult? qr = null;
        if (invoice.Status is not (InvoiceStatus.Cancelled or InvoiceStatus.Draft))
        {
            qr = PaymentQr.TryCreate(
                invoice.BankIban,
                invoice.BankBic,
                invoice.SellerName,
                invoice.Total,
                invoice.Currency,
                invoice.DueDate,
                invoice.VariableSymbol,
                invoice.ConstantSymbol,
                invoice.SpecificSymbol,
                paymentNote: invoice.Number);
        }

        var model = new InvoicePdfModel(
            invoice,
            template,
            company?.BankName,
            company?.InvoiceEmail ?? company?.Email,
            company?.Phone,
            company?.Website,
            company?.InvoiceFooter,
            legalClauses,
            qr,
            logoBytes);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(template == InvoicePdfTemplates.Modern ? 28 : 36);
                page.MarginVertical(template == InvoicePdfTemplates.Compact ? 28 : 32);
                page.DefaultTextStyle(x => x
                    .FontSize(9.5f)
                    .FontColor("#1A1D21")
                    .FontFamily("Arial"));

                page.Content().Element(c => Compose(c, model));
            });
        }).GeneratePdf();

        var fileName = $"faktura-{invoice.Number}.pdf";
        return ServiceResult<InvoicePdfFile>.Ok(new InvoicePdfFile(bytes, fileName));
    }

    private static void Compose(IContainer container, InvoicePdfModel model)
    {
        switch (model.Template)
        {
            case InvoicePdfTemplates.Modern:
                ModernInvoicePdf.Compose(container, model);
                break;
            case InvoicePdfTemplates.Compact:
                CompactInvoicePdf.Compose(container, model);
                break;
            default:
                ClassicInvoicePdf.Compose(container, model);
                break;
        }
    }
}
