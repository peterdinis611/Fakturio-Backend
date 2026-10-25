using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ISearchService
{
    Task<ServiceResult<GlobalSearchResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<EntitySearchResponse<CustomerResponse>>> SearchCustomersAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<ServiceResult<EntitySearchResponse<InvoiceListItemResponse>>> SearchInvoicesAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        InvoiceStatus? status = null,
        CancellationToken ct = default);
}
