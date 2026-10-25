using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceService
{
    Task<PagedResponse<InvoiceListItemResponse>> ListAsync(
        InvoiceListQuery query,
        CancellationToken ct = default);

    Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<InvoiceResponse?> GetByNumberAsync(string number, CancellationToken ct = default);

    Task<ServiceResult<InvoiceResponse>> CreateAsync(
        CreateInvoiceRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceResponse>> UpdateAsync(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceStatusResponse>> UpdateStatusAsync(
        Guid id,
        UpdateInvoiceStatusRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceStatusResponse>> MarkPaidAsync(
        Guid id,
        MarkInvoicePaidRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceStatusResponse>> CancelAsync(
        Guid id,
        CancelInvoiceRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceResponse>> DuplicateAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<InvoiceStatsResponse> GetStatsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<IReadOnlyList<InvoiceReminderItemResponse>> GetRemindersAsync(
        int withinDays,
        bool onlyOverdue,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceReminderItemResponse>> MarkReminderSentAsync(
        Guid id,
        CancellationToken ct = default);

    Task<SyncOverdueResponse> SyncOverdueAsync(CancellationToken ct = default);
}

public record InvoiceListQuery(
    int Page = 1,
    int PageSize = 20,
    InvoiceStatus? Status = null,
    Guid? CustomerId = null,
    string? Search = null,
    DateOnly? IssueFrom = null,
    DateOnly? IssueTo = null,
    bool? OverdueOnly = null
);
