using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Invoices;

public sealed class InvoiceIsdocService(AppDbContext db, ITenantContext tenant) : IInvoiceIsdocService
{
    public async Task<ServiceResult<InvoiceIsdocFile>> GenerateIsdocAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await GenerateForCompanyAsync(id, companyId, ct);
    }

    public async Task<ServiceResult<InvoiceIsdocFile>> GenerateForCompanyAsync(
        Guid id,
        Guid companyId,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<InvoiceIsdocFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        var xml = IsdocExport.Build(invoice);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
        var fileName = $"{invoice.Number}.isdoc";
        return ServiceResult<InvoiceIsdocFile>.Ok(new InvoiceIsdocFile(bytes, fileName));
    }
}
