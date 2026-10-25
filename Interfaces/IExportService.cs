using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IExportService
{
    Task<ServiceResult<ExportFile>> ExportInvoicesCsvAsync(
        DateOnly? from,
        DateOnly? to,
        InvoiceStatus? status,
        CancellationToken ct = default);

    Task<ServiceResult<ExportFile>> ExportVatCsvAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);
}

public record ExportFile(
    byte[] Content,
    string FileName,
    string ContentType = "text/csv; charset=utf-8");
