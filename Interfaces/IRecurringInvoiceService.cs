using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRecurringInvoiceService
{
    Task<PagedResponse<RecurringInvoiceListItemResponse>> ListAsync(
        int page,
        int pageSize,
        bool? activeOnly,
        CancellationToken ct = default);

    Task<RecurringInvoiceResponse?> GetAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<RecurringInvoiceResponse>> CreateAsync(
        CreateRecurringInvoiceRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<RecurringInvoiceResponse>> UpdateAsync(
        Guid id,
        UpdateRecurringInvoiceRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<InvoiceResponse>> GenerateNowAsync(Guid id, CancellationToken ct = default);

    /// <summary>System job: generate drafts for all due templates across tenants.</summary>
    Task<RecurringRunResult> ProcessDueAsync(CancellationToken ct = default);
}
