using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICustomerService
{
    Task<PagedResponse<CustomerResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    Task<IReadOnlyList<CustomerLookupItem>> LookupAsync(
        string? search,
        int limit = 20,
        CancellationToken ct = default);

    Task<CustomerResponse?> GetAsync(Guid id, CancellationToken ct = default);

    Task<CustomerResponse?> GetByIcoAsync(string ico, CancellationToken ct = default);

    Task<CustomerSummaryResponse?> GetSummaryAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<CustomerResponse>> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<CustomerResponse>> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
