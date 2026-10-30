using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICompanyRegistryService
{
    Task<ServiceResult<RegistryCompanyResponse>> LookupAsync(
        string country,
        string ico,
        CancellationToken ct = default);
}
